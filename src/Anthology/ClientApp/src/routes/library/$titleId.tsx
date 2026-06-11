import { createFileRoute, Navigate, Link } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../lib/auth'
import { useState } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import {
  getTitleOptions,
  getLibraryOptions,
  getSimilarOptions,
  getCreatorTitlesOptions,
  wantItemMutation,
  startItemMutation,
  finishItemMutation,
  abandonItemMutation,
  rateItemMutation,
} from '../../generated/@tanstack/react-query.gen'
import { Poster } from '../../components/poster'
import { StarRating } from '../../components/star-rating'
import { BackdropHero } from '../../components/backdrop-hero'
import { queryClient } from '../../lib/query-client'
import { cn, getErrorMessage } from '@/lib/utils'
import { toast } from 'sonner'

export const Route = createFileRoute('/library/$titleId')({
  // the shared-element morph needs the hero poster rendered at navigation commit,
  // so the title query must be resolved before the route renders (errors fall
  // through to the component's not-found handling)
  loader: ({ params }) =>
    queryClient
      .ensureQueryData(getTitleOptions({ path: { titleId: params.titleId } }))
      .catch(() => null),
  component: ItemDetailPage,
})

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

function ItemDetailPage() {
  const { titleId } = Route.useParams()
  const { user } = useAuth()
  const qc = useQueryClient()

  const invalidate = () => {
    qc.invalidateQueries({ predicate: (q) => (q.queryKey[0] as Record<string, unknown>)?._id === 'getLibrary' })
    qc.invalidateQueries({ predicate: (q) => (q.queryKey[0] as Record<string, unknown>)?._id === 'getTitle' })
  }

  const onError = (error: unknown) => toast.error(getErrorMessage(error))

  const { data: title, isLoading } = useQuery({
    ...getTitleOptions({ path: { titleId } }),
    enabled: !!user,
  })

  const { data: libraryData } = useQuery({
    ...getLibraryOptions({ query: { size: 100 } }),
    enabled: !!user,
  })

  const { data: similarTitles } = useQuery({
    ...getSimilarOptions({ path: { titleId } }),
    enabled: !!user,
  })

  const { data: creatorTitles } = useQuery({
    ...getCreatorTitlesOptions({ path: { titleId } }),
    enabled: !!user,
  })

  const libraryItem = libraryData?.items?.find((i) => i.titleId === titleId)

  const want = useMutation({
    ...wantItemMutation(),
    onSuccess: () => { invalidate(); toast.success('Added to library') },
    onError,
  })
  const start = useMutation({
    ...startItemMutation(),
    onSuccess: () => { invalidate(); toast.success('Started!') },
    onError,
  })
  const finish = useMutation({
    ...finishItemMutation(),
    onSuccess: () => { invalidate(); toast.success('Finished!') },
    onError,
  })
  const abandon = useMutation({
    ...abandonItemMutation(),
    onSuccess: () => { invalidate(); toast.success('Abandoned') },
    onError,
  })
  const rate = useMutation({
    ...rateItemMutation(),
    onSuccess: () => { invalidate(); toast.success('Rating updated') },
    onError,
  })

  const currentStatus = libraryItem?.status

  const handleRate = (rating: number) => {
    rate.mutate({ path: { titleId }, body: { rating } })
  }

  const handleWant = () => {
    want.mutate({ path: { titleId } })
  }

  const handleStart = () => {
    if (!currentStatus) {
      want.mutate({ path: { titleId } }, {
        onSuccess: () => start.mutate({ path: { titleId } }),
      })
    } else {
      start.mutate({ path: { titleId } })
    }
  }

  const handleFinish = () => {
    if (!currentStatus) {
      want.mutate({ path: { titleId } }, {
        onSuccess: () => finish.mutate({ path: { titleId } }),
      })
    } else {
      finish.mutate({ path: { titleId } })
    }
  }

  const handleAbandon = () => {
    if (!currentStatus) {
      want.mutate({ path: { titleId } }, {
        onSuccess: () => abandon.mutate({ path: { titleId } }),
      })
    } else {
      abandon.mutate({ path: { titleId } })
    }
  }

  if (!user) return <Navigate to="/login" />

  if (isLoading) return <DetailSkeleton titleId={titleId} />

  if (!title) {
    return (
      <div className="mx-auto max-w-4xl px-4 py-20 flex flex-col items-center justify-center">
        <p className="text-text-secondary">Title not found</p>
        <Link to="/library" className="text-teal text-[0.8125rem] mt-2 hover:text-teal-glow transition-colors">
          Back to library
        </Link>
      </div>
    )
  }

  const isTvShow = title.mediaType === 'tv_show'

  const actionLabels: { want: string; progress: string; finish: string; abandon: string } =
    ({
      film: { want: 'Want to watch', progress: 'Watching', finish: 'Finished', abandon: 'Abandon' },
      tv_show: { want: 'Want to watch', progress: 'Watching', finish: 'Finished', abandon: 'Abandon' },
      book: { want: 'Want to read', progress: 'Reading', finish: 'Finished', abandon: 'Abandon' },
      game: { want: 'Want to play', progress: 'Playing', finish: 'Finished', abandon: 'Abandon' },
      music: { want: 'Want to listen', progress: 'Listening', finish: 'Finished', abandon: 'Abandon' },
    } as Record<string, { want: string; progress: string; finish: string; abandon: string }>)[title.mediaType ?? 'film']
    ?? { want: 'Want', progress: 'In progress', finish: 'Finished', abandon: 'Abandon' }

  const accentClass: string =
    ({
      film: 'text-film-amber',
      tv_show: 'text-teal',
      book: 'text-book-sage',
      game: 'text-game-electric',
      music: 'text-music-violet',
    } as Record<string, string>)[title.mediaType ?? 'film']
    ?? 'text-teal'

  const posterAspect: '2/3' | '3/4' | '1/1' =
    ({
      film: '2/3', tv_show: '2/3', book: '3/4', game: '2/3', music: '1/1',
    } as Record<string, '2/3' | '3/4' | '1/1'>)[title.mediaType ?? 'film']
    ?? '2/3'

  const tvData = isTvShow ? (title as Record<string, unknown>) : null
  const seasons = (tvData?.seasons as Array<{
    titleId: string
    name: string
    seasonNumber: number
    episodes: Array<{
      titleId: string
      name: string
      episodeNumber: number
      airDate?: string | null
      stillPath?: string | null
    }>
  }>) ?? []

  return (
    <div className="relative">
      <BackdropHero backdropPath={title.backdropPath} posterPath={title.posterPath} />

      <div className="relative mx-auto max-w-4xl px-4 pb-6">
        <Link
          to="/library"
          className="inline-flex items-center gap-1.5 pt-6 text-[0.8125rem] text-text-secondary hover:text-text-primary transition-colors"
        >
          <svg viewBox="0 0 16 16" className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth={2}>
            <path d="M10 12L6 8l4-4" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          Library
        </Link>

        <div className="flex gap-6 mt-24 mb-8">
          <div className="w-48 shrink-0">
            <Poster
              path={title.posterPath}
              alt={title.name ?? ''}
              size="xl"
              aspect={posterAspect}
              viewTransitionName={`poster-${titleId}`}
              className="shadow-[var(--shadow-overlay)]"
            />
          </div>

          <motion.div
            className="flex-1 min-w-0 self-end pb-1"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
          >
            <h1 className="text-[2rem] font-bold tracking-tight text-text-primary leading-tight">
              {title.name}
            </h1>
            <div className="flex items-center gap-3 mt-2">
              {title.year && (
                <span className="text-[0.875rem] text-text-secondary">{String(title.year)}</span>
              )}
              <span className="text-[0.75rem] text-text-muted capitalize">
                {title.mediaType?.replace(/_/g, ' ')}
              </span>
              {isTvShow && !!tvData?.showData && (
                <span className="text-[0.75rem] text-text-muted">
                  {String((tvData!.showData as Record<string, unknown>).numberOfSeasons)} seasons
                </span>
              )}
            </div>

            {currentStatus && currentStatus !== 'want_to_consume' && (
              <div className="mt-4">
                <StarRating
                  value={libraryItem?.rating != null ? Number(libraryItem.rating) : null}
                  onChange={handleRate}
                  size="lg"
                  accentClass={accentClass}
                />
                {libraryItem?.rating != null && (
                  <span className="ml-2 text-[0.8125rem] text-text-secondary tabular-nums">
                    {Number(libraryItem.rating) / 2}/5
                  </span>
                )}
              </div>
            )}

            <div className="flex items-center gap-2 mt-5">
              <StatusButton
                label={actionLabels.want}
                active={currentStatus === 'want_to_consume'}
                onClick={handleWant}
                loading={want.isPending}
              />
              <StatusButton
                label={actionLabels.progress}
                active={currentStatus === 'in_progress'}
                onClick={handleStart}
                loading={want.isPending || start.isPending}
              />
              <StatusButton
                label={actionLabels.finish}
                active={currentStatus === 'finished'}
                onClick={handleFinish}
                loading={want.isPending || finish.isPending}
              />
              <StatusButton
                label={actionLabels.abandon}
                active={currentStatus === 'abandoned'}
                onClick={handleAbandon}
                loading={want.isPending || abandon.isPending}
                variant="ghost"
              />
            </div>

            {currentStatus && (
              <p className="text-[0.75rem] text-text-muted mt-3">
                Status: <span className="text-text-secondary">{getStatusLabel(currentStatus, title.mediaType)}</span>
                {libraryItem?.partsCompleted != null && libraryItem?.partsTotal != null && (
                  <span className="ml-2 tabular-nums">
                    ({String(libraryItem.partsCompleted)}/{String(libraryItem.partsTotal)} episodes)
                  </span>
                )}
              </p>
            )}
          </motion.div>
        </div>

        {title.overview && (
          <p className="text-[0.875rem] text-text-secondary leading-relaxed mb-8 max-w-prose">
            {title.overview}
          </p>
        )}

        {isTvShow && seasons.length > 0 && (
          <div className="mt-2">
            <h2 className="text-[1.125rem] font-semibold text-text-primary mb-4">Seasons</h2>
            <div className="flex flex-col gap-2">
              {seasons.map((season) => (
                <SeasonAccordion key={season.titleId} season={season} />
              ))}
            </div>
          </div>
        )}

        {/* view-transition-names must be unique per page: hero owns the current title, Similar wins ties over Creators */}
        <TitleRow titles={similarTitles ?? []} label="Similar" skipTransitionIds={new Set([titleId])} />
        <CreatorRow
          titles={creatorTitles ?? []}
          skipTransitionIds={new Set([titleId, ...(similarTitles ?? []).map((t) => t.titleId)])}
        />
      </div>
    </div>
  )
}

function StatusButton({
  label,
  active,
  onClick,
  loading,
  variant = 'default',
}: {
  label: string
  active: boolean
  onClick: () => void
  loading: boolean
  variant?: 'default' | 'ghost'
}) {
  return (
    <button
      onClick={onClick}
      disabled={loading}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-[0.8125rem] font-medium transition-all duration-150 active:scale-[0.97]',
        active && variant !== 'ghost' && 'bg-teal text-void shadow-[var(--shadow-glow)]',
        active && variant === 'ghost' && 'bg-danger/15 text-danger',
        !active && variant !== 'ghost' && 'bg-smoke text-text-secondary hover:bg-slate hover:text-text-primary',
        !active && variant === 'ghost' && 'text-text-muted hover:text-text-secondary hover:bg-smoke',
        loading && 'opacity-60 pointer-events-none',
      )}
    >
      {loading && (
        <span className="h-3 w-3 animate-spin rounded-full border-[1.5px] border-current border-t-transparent" />
      )}
      {label}
    </button>
  )
}

function SeasonAccordion({
  season,
}: {
  season: {
    titleId: string
    name: string
    seasonNumber: number
    episodes: Array<{
      titleId: string
      name: string
      episodeNumber: number
      airDate?: string | null
      stillPath?: string | null
    }>
  }
}) {
  const [open, setOpen] = useState(false)

  return (
    <div className="rounded-md bg-abyss overflow-hidden">
      <button
        onClick={() => setOpen(!open)}
        className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-smoke/30 transition-colors"
      >
        <div className="flex items-center gap-3">
          <span className="text-[0.875rem] font-medium text-text-primary">
            {season.name}
          </span>
          <span className="text-[0.75rem] text-text-muted">
            {season.episodes.length} episodes
          </span>
        </div>
        <svg
          viewBox="0 0 16 16"
          className={cn(
            'w-4 h-4 text-text-muted transition-transform duration-200',
            open && 'rotate-180',
          )}
          fill="none"
          stroke="currentColor"
          strokeWidth={2}
        >
          <path d="M4 6l4 4 4-4" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>

      <AnimatePresence initial={false}>
        {open && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.25, ease: [0.22, 1, 0.36, 1] }}
            className="overflow-hidden"
          >
            <div className="border-t border-border">
              {season.episodes.map((ep) => (
                <div
                  key={ep.titleId}
                  className="flex items-center gap-3 px-4 py-2.5 hover:bg-smoke/20 transition-colors"
                >
                  <span className="text-[0.75rem] text-text-muted tabular-nums w-6 shrink-0">
                    {String(ep.episodeNumber)}
                  </span>
                  <span className="text-[0.8125rem] text-text-secondary flex-1 truncate">
                    {ep.name}
                  </span>
                  {ep.airDate && (
                    <span className="text-[0.6875rem] text-text-muted shrink-0">
                      {ep.airDate}
                    </span>
                  )}
                </div>
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}

function TitleRow({ titles, label, skipTransitionIds }: { titles: Array<{ titleId?: string; name?: string; year?: null | number | string; posterPath?: null | string }>; label: string; skipTransitionIds?: Set<string | undefined> }) {
  if (!titles?.length) return null
  return (
    <div className="mt-8">
      <h2 className="text-[0.9375rem] font-semibold text-text-primary mb-4">{label}</h2>
      <div className="flex gap-3 overflow-x-auto pb-2 -mx-1 px-1 scrollbar-thin fade-edges-x">
        {titles.map((t) => (
          <Link key={t.titleId} to="/library/$titleId" params={{ titleId: t.titleId! }} className="shrink-0 w-[120px] group">
            <Poster
              path={t.posterPath}
              alt={t.name ?? ''}
              size="sm"
              aspect="2/3"
              viewTransitionName={skipTransitionIds?.has(t.titleId) ? undefined : `poster-${t.titleId}`}
            />
            <p className="text-[0.75rem] font-medium text-text-secondary group-hover:text-teal-glow transition-colors mt-1.5 line-clamp-2">{t.name}</p>
            {t.year && <p className="text-[0.6875rem] text-text-muted">{String(typeof t.year === 'string' ? t.year : t.year)}</p>}
          </Link>
        ))}
      </div>
    </div>
  )
}

function CreatorRow({ titles, skipTransitionIds }: { titles: Array<{ titleId?: string; name?: string; year?: null | number | string; posterPath?: null | string; sharedPerson?: string; sharedRole?: string }>; skipTransitionIds?: Set<string | undefined> }) {
  if (!titles?.length) return null
  return (
    <div className="mt-8">
      <h2 className="text-[0.9375rem] font-semibold text-text-primary mb-4">More from the Creators</h2>
      <div className="flex gap-3 overflow-x-auto pb-2 -mx-1 px-1 scrollbar-thin fade-edges-x">
        {titles.map((t) => (
          <Link key={t.titleId} to="/library/$titleId" params={{ titleId: t.titleId! }} className="shrink-0 w-[120px] group">
            <Poster
              path={t.posterPath}
              alt={t.name ?? ''}
              size="sm"
              aspect="2/3"
              viewTransitionName={skipTransitionIds?.has(t.titleId) ? undefined : `poster-${t.titleId}`}
            />
            <p className="text-[0.75rem] font-medium text-text-secondary group-hover:text-teal-glow transition-colors mt-1.5 line-clamp-2">{t.name}</p>
            {t.year && <p className="text-[0.6875rem] text-text-muted">{String(typeof t.year === 'string' ? t.year : t.year)}</p>}
            {t.sharedPerson && (
              <p className="text-[0.625rem] text-text-muted truncate">
                {t.sharedRole === 'director' ? 'Directed by' : t.sharedRole === 'actor' ? 'Starring' : 'By'} {t.sharedPerson}
              </p>
            )}
          </Link>
        ))}
      </div>
    </div>
  )
}

function DetailSkeleton({ titleId }: { titleId: string }) {
  return (
    <div className="relative">
      <div className="absolute inset-x-0 top-0 h-[420px] bg-abyss animate-skeleton" />
      <div className="relative mx-auto max-w-4xl px-4">
        <div className="h-4 w-16 rounded bg-smoke/40 animate-skeleton mt-6" />
        <div className="flex gap-6 mt-24">
          <div
            className="w-48 shrink-0 rounded-md bg-smoke/40 animate-skeleton"
            style={{ aspectRatio: '2/3', viewTransitionName: `poster-${titleId}` }}
          />
          <div className="flex-1 self-end pb-1 space-y-4">
            <div className="h-8 w-64 rounded bg-smoke/40 animate-skeleton" />
            <div className="h-4 w-32 rounded bg-smoke/40 animate-skeleton" />
            <div className="flex gap-2">
              {Array.from({ length: 4 }, (_, i) => (
                <div key={i} className="h-8 w-24 rounded-md bg-smoke/40 animate-skeleton" />
              ))}
            </div>
          </div>
        </div>
        <div className="space-y-2 mt-8">
          <div className="h-4 w-full rounded bg-abyss animate-skeleton" />
          <div className="h-4 w-3/4 rounded bg-abyss animate-skeleton" />
          <div className="h-4 w-1/2 rounded bg-abyss animate-skeleton" />
        </div>
      </div>
    </div>
  )
}
