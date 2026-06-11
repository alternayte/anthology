import { createFileRoute, Navigate, Link } from '@tanstack/react-router'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { motion } from 'motion/react'
import { useAuth } from '../lib/auth'
import { useEffect, useMemo, useRef } from 'react'
import { getDiaryInfiniteOptions, getLibraryOptions } from '../generated/@tanstack/react-query.gen'
import { Poster } from '../components/poster'
import { StarRating } from '../components/star-rating'

export const Route = createFileRoute('/diary')({
  component: DiaryPage,
})

const statusLabels: Record<string, string> = {
  want_to_consume: 'Added',
  in_progress: 'Started',
  finished: 'Finished',
  abandoned: 'Abandoned',
  rerated: 'Rerated',
}

function DiaryPage() {
  const { user } = useAuth()

  const { data, isLoading, fetchNextPage, hasNextPage, isFetchingNextPage } = useInfiniteQuery({
    ...getDiaryInfiniteOptions({ query: { size: 20 } }),
    initialPageParam: undefined as unknown as string,
    getNextPageParam: (lastPage) =>
      lastPage.nextCursor ? { query: { cursor: lastPage.nextCursor } } : undefined,
    enabled: !!user,
  })

  const { data: libraryData } = useQuery({
    ...getLibraryOptions({ query: { size: 100 } }),
    enabled: !!user,
  })

  const titlesById = useMemo(
    () => new Map((libraryData?.items ?? []).map((i) => [i.titleId, i])),
    [libraryData],
  )

  const entries = data?.pages.flatMap((p) => p.items ?? []) ?? []

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

  if (!user) return <Navigate to="/login" />

  return (
    <div className="mx-auto max-w-6xl px-4 py-6">
      <h1 className="text-[1.5rem] font-semibold tracking-tight text-text-primary mb-6">Diary</h1>

      {isLoading && (
        <div className="flex flex-col gap-2">
          {Array.from({ length: 8 }, (_, i) => (
            <div key={i} className="flex items-center gap-4 py-2.5 px-3">
              <div className="h-4 w-24 rounded bg-abyss animate-skeleton" />
              <div className="h-10 w-7 rounded bg-abyss animate-skeleton" />
              <div className="h-4 w-48 rounded bg-abyss animate-skeleton" />
              <div className="flex-1" />
              <div className="h-4 w-16 rounded bg-abyss animate-skeleton" />
            </div>
          ))}
        </div>
      )}

      {!isLoading && entries.length === 0 && (
        <motion.div
          className="flex flex-col items-center justify-center py-20"
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
        >
          <p className="text-text-secondary text-[0.9375rem] font-medium mb-1">No diary entries yet</p>
          <p className="text-text-muted text-[0.8125rem]">Your activity will appear here as you track media.</p>
        </motion.div>
      )}

      <div className="flex flex-col">
        {entries.map((entry, i) => {
          const item = titlesById.get(entry.titleId)
          return (
            <motion.div
              key={`${entry.titleId}-${entry.occurredAt}`}
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] as const, delay: Math.min((i % 20) * 0.02, 0.3) }}
            >
              <Link
                to="/library/$titleId"
                params={{ titleId: entry.titleId }}
                className="flex items-center gap-4 py-2.5 px-3 -mx-3 rounded-md hover:bg-smoke/30 transition-colors group"
              >
                <span className="text-[0.75rem] text-text-muted w-24 shrink-0 tabular-nums">
                  {new Date(entry.occurredAt).toLocaleDateString(undefined, {
                    month: 'short',
                    day: 'numeric',
                    year: 'numeric',
                  })}
                </span>
                <div className="w-7 shrink-0">
                  <Poster path={item?.posterPath} alt={item?.title ?? ''} size="sm" />
                </div>
                <span className="text-[0.8125rem] text-text-primary font-medium flex-1 truncate group-hover:text-teal-glow transition-colors">
                  {item?.title ?? 'Untitled'}
                </span>
                <div className="flex items-center gap-3 shrink-0">
                  {entry.rating != null && (
                    <StarRating value={Number(entry.rating)} readonly size="sm" accentClass="text-film-amber" />
                  )}
                  <span className="text-[0.6875rem] text-text-muted">
                    {statusLabels[entry.status] ?? entry.status}
                  </span>
                </div>
              </Link>
            </motion.div>
          )
        })}
      </div>

      <div ref={sentinelRef} className="h-px" />
      {isFetchingNextPage && (
        <div className="flex justify-center py-4">
          <div className="h-4 w-4 animate-spin rounded-full border-2 border-ash border-t-teal" />
        </div>
      )}
    </div>
  )
}
