import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../lib/auth'
import { useState } from 'react'
import { getDiaryOptions } from '../generated/@tanstack/react-query.gen'
import { StarRating } from '../components/star-rating'
import { cn } from '@/lib/utils'

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
  const [cursor, setCursor] = useState<string | undefined>()

  if (!user) return <Navigate to="/login" />

  const { data, isLoading } = useQuery({
    ...getDiaryOptions({ query: { cursor, size: 20 } }),
  })

  return (
    <div>
      <h1 className="text-[1.5rem] font-semibold tracking-tight text-text-primary mb-6">Diary</h1>

      {isLoading && (
        <div className="flex flex-col gap-2">
          {Array.from({ length: 8 }, (_, i) => (
            <div key={i} className="flex items-center gap-4 py-3 px-3 rounded-md bg-abyss animate-skeleton">
              <div className="h-4 w-24 rounded bg-smoke" />
              <div className="flex-1" />
              <div className="h-4 w-16 rounded bg-smoke" />
            </div>
          ))}
        </div>
      )}

      {!isLoading && data?.items?.length === 0 && (
        <div className="flex flex-col items-center justify-center py-20">
          <p className="text-text-secondary text-[0.9375rem] font-medium mb-1">No diary entries yet</p>
          <p className="text-text-muted text-[0.8125rem]">Your activity will appear here as you track media.</p>
        </div>
      )}

      <div className="flex flex-col">
        {data?.items?.map((entry, i) => (
          <div
            key={i}
            className={cn(
              'flex items-center gap-4 py-3 px-3 -mx-3 rounded-md hover:bg-smoke/30 transition-colors',
            )}
          >
            <span className="text-[0.75rem] text-text-muted w-24 shrink-0 tabular-nums">
              {new Date(entry.occurredAt).toLocaleDateString(undefined, {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
              })}
            </span>
            <span className="text-[0.8125rem] text-text-secondary flex-1 truncate">
              {entry.titleId.slice(0, 8)}
            </span>
            <div className="flex items-center gap-3 shrink-0">
              {entry.rating != null && (
                <StarRating value={Number(entry.rating)} readonly size="sm" accentClass="text-film-amber" />
              )}
              <span className="text-[0.6875rem] text-text-muted">
                {statusLabels[entry.status] ?? entry.status}
              </span>
            </div>
          </div>
        ))}
      </div>

      {data?.nextCursor && (
        <div className="mt-8 flex justify-center">
          <button
            onClick={() => setCursor(data.nextCursor!)}
            className="rounded-md bg-smoke px-5 py-2.5 text-[0.8125rem] font-medium text-text-secondary hover:bg-slate hover:text-text-primary transition-colors"
          >
            Load more
          </button>
        </div>
      )}
    </div>
  )
}
