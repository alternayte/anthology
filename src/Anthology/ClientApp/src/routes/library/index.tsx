import { createFileRoute, Link, Navigate } from '@tanstack/react-router'
import { useInfiniteQuery } from '@tanstack/react-query'
import { motion } from 'motion/react'
import { useAuth } from '../../lib/auth'
import { useEffect, useRef, useState } from 'react'
import { getLibraryInfiniteOptions } from '../../generated/@tanstack/react-query.gen'
import { Poster } from '../../components/poster'
import { StarRating } from '../../components/star-rating'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/library/')({
  component: LibraryPage,
})

const mediaTypes = [
  { value: '', label: 'All', color: 'bg-teal/15 text-teal' },
  { value: 'Film', label: 'Films', color: 'bg-film-amber/15 text-film-amber' },
  { value: 'TvShow', label: 'TV Shows', color: 'bg-teal/15 text-teal' },
  { value: 'Book', label: 'Books', color: 'bg-book-sage/15 text-book-sage' },
  { value: 'Game', label: 'Games', color: 'bg-game-electric/15 text-game-electric' },
  { value: 'Music', label: 'Music', color: 'bg-music-violet/15 text-music-violet' },
] as const

const statuses = [
  { value: '', label: 'All statuses' },
  { value: 'WantToConsume', label: 'Want to watch' },
  { value: 'InProgress', label: 'Watching' },
  { value: 'Finished', label: 'Finished' },
  { value: 'Abandoned', label: 'Abandoned' },
] as const

const sortOptions = [
  { value: 'added', label: 'Date added' },
  { value: 'rating', label: 'Rating' },
  { value: 'title', label: 'Title' },
] as const

const statusLabelsByMedia: Record<string, Record<string, string>> = {
  film: { want_to_consume: 'Want to watch', in_progress: 'Watching', finished: 'Finished', abandoned: 'Abandoned' },
  tv_show: { want_to_consume: 'Want to watch', in_progress: 'Watching', finished: 'Finished', abandoned: 'Abandoned' },
  book: { want_to_consume: 'Want to read', in_progress: 'Reading', finished: 'Finished', abandoned: 'Abandoned' },
  game: { want_to_consume: 'Want to play', in_progress: 'Playing', finished: 'Finished', abandoned: 'Abandoned' },
  music: { want_to_consume: 'Want to listen', in_progress: 'Listening', finished: 'Finished', abandoned: 'Abandoned' },
}

function getStatusLabel(status: string, mediaType?: string): string {
  const labels = statusLabelsByMedia[mediaType ?? 'film'] ?? statusLabelsByMedia.film
  return labels[status] ?? status
}

const posterAspect: Record<string, '2/3' | '3/4' | '1/1'> = {
  film: '2/3', tv_show: '2/3', book: '3/4', game: '2/3', music: '1/1',
}

const statusStyles: Record<string, string> = {
  want_to_consume: 'bg-teal/15 text-teal-glow',
  in_progress: 'bg-film-amber/15 text-film-amber',
  finished: 'bg-success/15 text-success',
  abandoned: 'bg-ash/20 text-text-muted',
}

const entrance = (i: number) => ({
  initial: { opacity: 0, y: 14 },
  animate: { opacity: 1, y: 0 },
  transition: { duration: 0.35, ease: [0.22, 1, 0.36, 1] as const, delay: Math.min((i % 40) * 0.02, 0.4) },
})

function LibraryPage() {
  const { user } = useAuth()
  const [view, setView] = useState<'grid' | 'list'>('grid')
  const [sort, setSort] = useState('added')
  const [statusFilter, setStatusFilter] = useState('')
  const [mediaFilter, setMediaFilter] = useState('')
  const [searchTerm, setSearchTerm] = useState('')

  if (!user) return <Navigate to="/login" />

  const { data, isLoading, fetchNextPage, hasNextPage, isFetchingNextPage } = useInfiniteQuery({
    ...getLibraryInfiniteOptions({
      query: {
        sort,
        dir: 'desc',
        size: 40,
        ...(statusFilter && { status: statusFilter }),
        ...(mediaFilter && { media: mediaFilter }),
      },
    }),
    initialPageParam: undefined as unknown as string,
    getNextPageParam: (lastPage) =>
      lastPage.nextCursor ? { query: { cursor: lastPage.nextCursor } } : undefined,
  })

  const items = data?.pages.flatMap((p) => p.items ?? []) ?? []
  const filtered = searchTerm
    ? items.filter((item) =>
        item.title?.toLowerCase().includes(searchTerm.toLowerCase()),
      )
    : items

  const sentinelRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const el = sentinelRef.current
    if (!el) return
    const obs = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && hasNextPage && !isFetchingNextPage) fetchNextPage()
      },
      { rootMargin: '400px' },
    )
    obs.observe(el)
    return () => obs.disconnect()
  }, [hasNextPage, isFetchingNextPage, fetchNextPage])

  const resetFilters = () => {
    setStatusFilter('')
    setMediaFilter('')
    setSearchTerm('')
  }

  const hasActiveFilters = statusFilter || mediaFilter || searchTerm

  return (
    <div className="mx-auto max-w-6xl px-4 py-6">
      {/* Header */}
      <div className="flex items-end justify-between mb-6">
        <div>
          <h1 className="text-[1.5rem] font-semibold tracking-tight text-text-primary">
            Library
          </h1>
          {items.length > 0 && (
            <p className="text-[0.8125rem] text-text-muted mt-0.5">
              {items.length} titles
            </p>
          )}
        </div>
        <div className="flex items-center gap-1">
          <button
            onClick={() => setView('grid')}
            className={cn(
              'p-2 rounded-md transition-colors',
              view === 'grid' ? 'text-text-primary bg-smoke' : 'text-text-muted hover:text-text-secondary',
            )}
            aria-label="Grid view"
          >
            <svg viewBox="0 0 16 16" className="w-4 h-4" fill="currentColor">
              <rect x="1" y="1" width="6" height="6" rx="1" />
              <rect x="9" y="1" width="6" height="6" rx="1" />
              <rect x="1" y="9" width="6" height="6" rx="1" />
              <rect x="9" y="9" width="6" height="6" rx="1" />
            </svg>
          </button>
          <button
            onClick={() => setView('list')}
            className={cn(
              'p-2 rounded-md transition-colors',
              view === 'list' ? 'text-text-primary bg-smoke' : 'text-text-muted hover:text-text-secondary',
            )}
            aria-label="List view"
          >
            <svg viewBox="0 0 16 16" className="w-4 h-4" fill="currentColor">
              <rect x="1" y="2" width="14" height="2" rx="0.5" />
              <rect x="1" y="7" width="14" height="2" rx="0.5" />
              <rect x="1" y="12" width="14" height="2" rx="0.5" />
            </svg>
          </button>
        </div>
      </div>

      {/* Filter bar */}
      <div className="flex flex-wrap items-center gap-2 mb-6">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
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
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Search your library..."
            className="w-full rounded-md bg-smoke border border-transparent pl-9 pr-3 py-2 text-[0.8125rem] text-text-primary placeholder:text-text-muted focus:border-teal focus:shadow-[var(--shadow-glow)] focus:outline-none transition-[border-color,box-shadow] duration-150"
          />
        </div>

        <div className="flex items-center gap-1.5">
          {mediaTypes.map((mt) => (
            <button
              key={mt.value}
              onClick={() => setMediaFilter(mt.value)}
              className={cn(
                'rounded-full px-3 py-1.5 text-[0.8125rem] font-medium transition-all duration-150 active:scale-[0.97]',
                mediaFilter === mt.value
                  ? mt.color
                  : 'text-text-muted hover:text-text-secondary hover:bg-smoke',
              )}
            >
              {mt.label}
            </button>
          ))}
        </div>

        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="rounded-md bg-smoke border-none px-3 py-2 text-[0.8125rem] text-text-secondary focus:outline-none focus:ring-1 focus:ring-teal cursor-pointer"
        >
          {statuses.map((s) => (
            <option key={s.value} value={s.value}>{s.label}</option>
          ))}
        </select>

        <select
          value={sort}
          onChange={(e) => setSort(e.target.value)}
          className="rounded-md bg-smoke border-none px-3 py-2 text-[0.8125rem] text-text-secondary focus:outline-none focus:ring-1 focus:ring-teal cursor-pointer"
        >
          {sortOptions.map((s) => (
            <option key={s.value} value={s.value}>{s.label}</option>
          ))}
        </select>
      </div>

      {/* Loading skeleton */}
      {isLoading && (
        view === 'grid' ? <GridSkeleton /> : <ListSkeleton />
      )}

      {/* Empty state */}
      {!isLoading && items.length === 0 && !hasActiveFilters && (
        <motion.div
          className="flex flex-col items-center justify-center py-20"
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
        >
          <svg viewBox="0 0 24 24" className="w-12 h-12 text-ash mb-4" fill="none" stroke="currentColor" strokeWidth={1}>
            <path d="M4 19.5A2.5 2.5 0 016.5 17H20V4H6.5A2.5 2.5 0 004 6.5v13z" strokeLinecap="round" strokeLinejoin="round" />
            <path d="M8 7h8M8 11h6" strokeLinecap="round" />
          </svg>
          <p className="text-text-secondary text-[0.9375rem] font-medium mb-3">Your collection starts here</p>
          <Link
            to="/search"
            className="rounded-md bg-teal px-4 py-2 text-[0.8125rem] font-medium text-void hover:bg-teal-glow transition-all duration-150 active:scale-[0.97]"
          >
            Search for something to add
          </Link>
        </motion.div>
      )}

      {/* No results for filters */}
      {!isLoading && filtered.length === 0 && hasActiveFilters && items.length > 0 && (
        <div className="flex flex-col items-center justify-center py-16">
          <p className="text-text-secondary text-[0.9375rem] mb-2">Nothing matches these filters</p>
          <button
            onClick={resetFilters}
            className="text-teal text-[0.8125rem] hover:text-teal-glow transition-colors"
          >
            Clear filters
          </button>
        </div>
      )}

      {/* Grid view */}
      {!isLoading && filtered.length > 0 && view === 'grid' && (
        <div className="grid grid-cols-[repeat(auto-fill,minmax(120px,1fr))] gap-2">
          {filtered.map((item, i) => (
            <motion.div key={item.titleId} {...entrance(i)}>
              <Link
                to="/library/$titleId"
                params={{ titleId: item.titleId! }}
                className="group relative block transition-transform duration-150 active:scale-[0.97]"
              >
                <Poster
                  path={item.posterPath}
                  alt={item.title ?? ''}
                  size="lg"
                  aspect={posterAspect[item.mediaType ?? 'film'] ?? '2/3'}
                  viewTransitionName={`poster-${item.titleId}`}
                  className="transition-all duration-200 ease-out group-hover:shadow-[var(--shadow-hover-lift)] group-hover:scale-[1.02]"
                />
                <div className="absolute inset-0 rounded-md bg-void/60 opacity-0 group-hover:opacity-100 transition-opacity duration-200 flex flex-col justify-end p-2.5">
                  <p className="text-[0.75rem] font-medium text-text-primary leading-tight line-clamp-2">
                    {item.title}
                  </p>
                  {item.rating != null && (
                    <div className="mt-1">
                      <StarRating value={Number(item.rating)} readonly size="sm" accentClass="text-film-amber" />
                    </div>
                  )}
                  {item.status && (
                    <span className={cn(
                      'mt-1.5 inline-block self-start rounded px-1.5 py-0.5 text-[0.625rem] font-medium',
                      statusStyles[item.status] ?? 'bg-ash/20 text-text-muted',
                    )}>
                      {getStatusLabel(item.status, item.mediaType)}
                    </span>
                  )}
                </div>
              </Link>
            </motion.div>
          ))}
        </div>
      )}

      {/* List view */}
      {!isLoading && filtered.length > 0 && view === 'list' && (
        <div className="flex flex-col">
          {filtered.map((item, i) => (
            <motion.div key={item.titleId} {...entrance(i)}>
              <Link
                to="/library/$titleId"
                params={{ titleId: item.titleId! }}
                className="flex items-center gap-4 py-3 px-2 -mx-2 rounded-md hover:bg-smoke/50 transition-colors group"
              >
                <div className="w-10 shrink-0">
                  <Poster
                    path={item.posterPath}
                    alt={item.title ?? ''}
                    size="sm"
                  />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-[0.875rem] font-medium text-text-primary truncate group-hover:text-teal-glow transition-colors">
                    {item.title}
                  </p>
                  <p className="text-[0.75rem] text-text-muted capitalize">
                    {item.mediaType?.replace(/_/g, ' ')}
                  </p>
                </div>
                <div className="flex items-center gap-3 shrink-0">
                  {item.partsCompleted != null && item.partsTotal != null && (
                    <span className="text-[0.75rem] text-text-muted tabular-nums">
                      {String(item.partsCompleted)}/{String(item.partsTotal)}
                    </span>
                  )}
                  {item.rating != null && (
                    <StarRating value={Number(item.rating)} readonly size="sm" accentClass="text-film-amber" />
                  )}
                  {item.status && (
                    <span className={cn(
                      'rounded px-2 py-0.5 text-[0.6875rem] font-medium',
                      statusStyles[item.status] ?? 'bg-ash/20 text-text-muted',
                    )}>
                      {getStatusLabel(item.status, item.mediaType)}
                    </span>
                  )}
                </div>
              </Link>
            </motion.div>
          ))}
        </div>
      )}

      {/* Infinite scroll sentinel + fetching indicator */}
      <div ref={sentinelRef} className="h-px" />
      {isFetchingNextPage && (
        view === 'grid' ? (
          <div className="mt-2 grid grid-cols-[repeat(auto-fill,minmax(120px,1fr))] gap-2">
            {Array.from({ length: 8 }, (_, i) => (
              <div key={i} className="rounded-md bg-abyss animate-skeleton" style={{ aspectRatio: '2/3' }} />
            ))}
          </div>
        ) : (
          <div className="mt-2 flex justify-center py-4">
            <div className="h-4 w-4 animate-spin rounded-full border-2 border-ash border-t-teal" />
          </div>
        )
      )}
    </div>
  )
}

function GridSkeleton() {
  return (
    <div className="grid grid-cols-[repeat(auto-fill,minmax(120px,1fr))] gap-2">
      {Array.from({ length: 24 }, (_, i) => (
        <div key={i} className="rounded-md bg-abyss animate-skeleton" style={{ aspectRatio: '2/3' }} />
      ))}
    </div>
  )
}

function ListSkeleton() {
  return (
    <div className="flex flex-col gap-1">
      {Array.from({ length: 12 }, (_, i) => (
        <div key={i} className="flex items-center gap-4 py-3 px-2">
          <div className="w-10 h-15 rounded bg-abyss animate-skeleton" />
          <div className="flex-1 space-y-2">
            <div className="h-3.5 w-48 rounded bg-abyss animate-skeleton" />
            <div className="h-3 w-24 rounded bg-abyss animate-skeleton" />
          </div>
        </div>
      ))}
    </div>
  )
}
