import { createFileRoute, Navigate, useNavigate } from '@tanstack/react-router'
import type { SearchSchemaInput } from '@tanstack/react-router'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useAuth } from '../lib/auth'
import { useState, useMemo } from 'react'
import { searchCatalogOptions, searchLocalOptions, addTitleMutation } from '../generated/@tanstack/react-query.gen'
import type { CatalogSearchResult, LocalSearchResult } from '../generated/types.gen'
import { Poster } from '../components/poster'
import { cn, getErrorMessage } from '@/lib/utils'
import { toast } from 'sonner'

type SearchParams = { term: string; media: string }

export const Route = createFileRoute('/search')({
  validateSearch: (search: Record<string, unknown> & SearchSchemaInput): SearchParams => ({
    term: typeof search.term === 'string' ? search.term : '',
    media: typeof search.media === 'string' ? search.media : '',
  }),
  component: SearchPage,
})

const mediaFilters = [
  { value: '', label: 'All', color: 'bg-teal/15 text-teal' },
  { value: 'Film', label: 'Film', color: 'bg-film-amber/15 text-film-amber' },
  { value: 'TvShow', label: 'TV', color: 'bg-teal/15 text-teal' },
  { value: 'Book', label: 'Book', color: 'bg-book-sage/15 text-book-sage' },
  { value: 'Game', label: 'Game', color: 'bg-game-electric/15 text-game-electric' },
  { value: 'Music', label: 'Music', color: 'bg-music-violet/15 text-music-violet' },
] as const

const mediaTypeLabels: Record<string, string> = {
  film: 'Films',
  tv_show: 'TV Shows',
  book: 'Books',
  game: 'Games',
  music: 'Music',
}

const mediaBadgeColors: Record<string, string> = {
  film: 'bg-film-amber/15 text-film-amber',
  tv_show: 'bg-teal/15 text-teal',
  book: 'bg-book-sage/15 text-book-sage',
  game: 'bg-game-electric/15 text-game-electric',
  music: 'bg-music-violet/15 text-music-violet',
}

const posterAspect: Record<string, '2/3' | '3/4' | '1/1'> = {
  film: '2/3',
  tv_show: '2/3',
  book: '3/4',
  game: '2/3',
  music: '1/1',
}

function SearchPage() {
  const { user } = useAuth()
  const navigate = useNavigate({ from: '/search' })
  const { term: searchTerm, media: mediaFilter } = Route.useSearch()
  const [inputValue, setInputValue] = useState(searchTerm)

  if (!user) return <Navigate to="/login" />

  const submitSearch = (newTerm: string, newMedia?: string) => {
    navigate({
      search: {
        term: newTerm || undefined,
        media: (newMedia !== undefined ? newMedia : mediaFilter) || undefined,
      } as SearchParams,
    })
  }

  const { data: providerResults, isLoading: isLoadingProvider } = useQuery({
    ...searchCatalogOptions({
      query: { term: searchTerm, ...(mediaFilter && { mediaType: mediaFilter }) },
    }),
    enabled: searchTerm.length > 0,
  })

  const { data: localResults, isLoading: isLoadingLocal } = useQuery({
    ...searchLocalOptions({
      query: { term: searchTerm, ...(mediaFilter && { mediaType: mediaFilter }) },
    }),
    enabled: searchTerm.length > 0,
  })

  const isLoading = isLoadingProvider || isLoadingLocal

  const mergedResults = useMemo<UnifiedResult[]>(() => {
    const local: UnifiedResult[] = (localResults ?? []).map((r: LocalSearchResult) => ({
      id: r.titleId,
      titleId: r.titleId,
      name: r.name,
      year: typeof r.year === 'string' ? parseInt(r.year, 10) : r.year,
      posterUrl: r.posterPath,
      overview: r.overview,
      mediaType: r.mediaType,
      isLocal: true,
    }))

    const localTitleIds = new Set(local.map((r) => r.titleId))

    const provider: UnifiedResult[] = (providerResults ?? [])
      .filter((r: CatalogSearchResult) => !localTitleIds.has(r.externalId))
      .map((r: CatalogSearchResult) => ({
        id: r.externalId,
        externalId: r.externalId,
        name: r.name,
        year: typeof r.year === 'string' ? parseInt(r.year, 10) : r.year,
        posterUrl: r.posterUrl,
        overview: r.overview,
        mediaType: r.mediaType,
        isLocal: false,
      }))

    return [...local, ...provider]
  }, [localResults, providerResults])

  const addMutation = useMutation({
    ...addTitleMutation(),
    onSuccess: (data) => {
      if (data?.titleId) navigate({ to: '/library/$titleId', params: { titleId: data.titleId } })
    },
    onError: (error: unknown) => toast.error(getErrorMessage(error)),
  })

  const grouped = groupByMediaType(mergedResults)
  const showGroups = !mediaFilter

  return (
    <div>
      <h1 className="text-[1.5rem] font-semibold tracking-tight text-text-primary mb-6">Search</h1>

      <form onSubmit={e => { e.preventDefault(); submitSearch(inputValue) }} className="flex gap-2 mb-4">
        <div className="relative flex-1 max-w-lg">
          <svg
            viewBox="0 0 24 24"
            className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-muted"
            fill="none"
            stroke="currentColor"
            strokeWidth={2}
          >
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.3-4.3" />
          </svg>
          <input
            value={inputValue}
            onChange={e => setInputValue(e.target.value)}
            placeholder="Search films, books, games, music..."
            className="w-full rounded-md bg-smoke border border-transparent pl-9 pr-3 py-2.5 text-[0.875rem] text-text-primary placeholder:text-text-muted focus:border-teal focus:outline-none transition-colors"
          />
        </div>
        <button
          type="submit"
          className="rounded-md bg-teal px-4 py-2.5 text-[0.8125rem] font-medium text-void hover:bg-teal-glow transition-colors"
        >
          Search
        </button>
      </form>

      {/* Media type filter chips */}
      <div className="flex items-center gap-1.5 mb-6">
        {mediaFilters.map((f) => (
          <button
            key={f.value}
            onClick={() => submitSearch(searchTerm, f.value)}
            className={cn(
              'rounded-full px-3 py-1.5 text-[0.8125rem] font-medium transition-colors',
              mediaFilter === f.value
                ? f.color
                : 'text-text-muted hover:text-text-secondary hover:bg-smoke',
            )}
          >
            {f.label}
          </button>
        ))}
      </div>

      {isLoading && (
        <div className="flex flex-col gap-3">
          {Array.from({ length: 5 }, (_, i) => (
            <div key={i} className="flex items-center gap-4 p-4 rounded-md bg-abyss animate-skeleton">
              <div className="w-12 h-[72px] rounded bg-smoke" />
              <div className="flex-1 space-y-2">
                <div className="h-4 w-48 rounded bg-smoke" />
                <div className="h-3 w-24 rounded bg-smoke" />
              </div>
            </div>
          ))}
        </div>
      )}

      {!isLoading && searchTerm && mergedResults.length === 0 && (
        <p className="text-text-muted text-[0.875rem]">No results for &ldquo;{searchTerm}&rdquo;</p>
      )}

      {!isLoading && mergedResults.length > 0 && (
        showGroups ? (
          <div className="flex flex-col gap-6">
            {Object.entries(grouped).map(([type, items]) => (
              items.length > 0 && (
                <div key={type}>
                  <h2 className="text-[0.8125rem] font-semibold text-text-secondary uppercase tracking-wider mb-3">
                    {mediaTypeLabels[type] ?? type}
                  </h2>
                  <div className="flex flex-col gap-2">
                    {items.map((r) => (
                      <SearchResultCard
                        key={r.id}
                        result={r}
                        isLocal={r.isLocal}
                        onAdd={addMutation.mutate}
                        onNavigate={(titleId) => navigate({ to: '/library/$titleId', params: { titleId } })}
                      />
                    ))}
                  </div>
                </div>
              )
            ))}
          </div>
        ) : (
          <div className="flex flex-col gap-2">
            {mergedResults.map((r) => (
              <SearchResultCard
                key={r.id}
                result={r}
                isLocal={r.isLocal}
                onAdd={addMutation.mutate}
                onNavigate={(titleId) => navigate({ to: '/library/$titleId', params: { titleId } })}
              />
            ))}
          </div>
        )
      )}
    </div>
  )
}

function SearchResultCard({
  result: r,
  isLocal,
  onAdd,
  onNavigate,
}: {
  result: { id: string; titleId?: string; externalId?: string; name: string; year: number | null; posterUrl: string | null; overview: string | null; mediaType: string; isLocal: boolean }
  isLocal: boolean
  onAdd: (opts: { body: { externalId: string } }) => void
  onNavigate: (titleId: string) => void
}) {
  const aspect = posterAspect[r.mediaType ?? 'film'] ?? '2/3'

  const handleClick = () => {
    if (isLocal && r.titleId) {
      onNavigate(r.titleId)
    } else if (r.externalId) {
      onAdd({ body: { externalId: r.externalId } })
    }
  }

  return (
    <div
      onClick={handleClick}
      className="cursor-pointer rounded-md bg-abyss p-4 hover:bg-slate transition-colors flex items-center gap-4 group"
    >
      <div className="w-12 shrink-0">
        <Poster path={r.posterUrl} alt={r.name ?? ''} size="sm" aspect={aspect} />
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <p className="text-[0.875rem] font-medium text-text-primary group-hover:text-teal-glow transition-colors truncate">
            {r.name}
          </p>
          {r.mediaType && (
            <span className={cn(
              'rounded px-1.5 py-0.5 text-[0.625rem] font-medium shrink-0',
              mediaBadgeColors[r.mediaType] ?? 'bg-ash/20 text-text-muted',
            )}>
              {r.mediaType.replace(/_/g, ' ')}
            </span>
          )}
          {isLocal && (
            <span className="rounded px-1.5 py-0.5 text-[0.625rem] font-medium bg-teal/10 text-teal shrink-0">In library</span>
          )}
        </div>
        <p className="text-[0.75rem] text-text-muted">{r.year ? String(r.year) : ''}</p>
        {r.overview && (
          <p className="text-[0.75rem] text-text-muted mt-1 line-clamp-2">{r.overview}</p>
        )}
      </div>
    </div>
  )
}

type UnifiedResult = {
  id: string
  titleId?: string
  externalId?: string
  name: string
  year: number | null
  posterUrl: string | null
  overview: string | null
  mediaType: string
  isLocal: boolean
}

function groupByMediaType(results: UnifiedResult[]): Record<string, UnifiedResult[]> {
  const order = ['film', 'tv_show', 'book', 'game', 'music']
  const groups: Record<string, UnifiedResult[]> = {}
  for (const type of order) groups[type] = []
  for (const r of results) {
    const type = r.mediaType ?? 'film'
    if (!groups[type]) groups[type] = []
    groups[type].push(r)
  }
  return groups
}
