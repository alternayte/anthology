import { createFileRoute, Navigate, Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../lib/auth'
import {
  getHiddenTitlesOptions,
  getHiddenTitlesQueryKey,
  submitFeedbackMutation,
} from '../../generated/@tanstack/react-query.gen'
import { Poster } from '../../components/poster'
import { cn, getErrorMessage } from '@/lib/utils'
import { toast } from 'sonner'

export const Route = createFileRoute('/settings/hidden')({
  component: HiddenTitlesPage,
})

function HiddenTitlesPage() {
  const { user } = useAuth()
  const qc = useQueryClient()
  const [pendingIds, setPendingIds] = useState<Set<string>>(new Set())

  const { data: hidden, isLoading } = useQuery({
    ...getHiddenTitlesOptions(),
    enabled: !!user,
  })

  const onError = (error: unknown) => toast.error(getErrorMessage(error))

  const markPending = (id: string, on: boolean) =>
    setPendingIds(prev => { const next = new Set(prev); on ? next.add(id) : next.delete(id); return next })

  const restore = useMutation({
    ...submitFeedbackMutation(),
    onError,
  })

  const handleRestore = (titleId: string) => {
    markPending(titleId, true)
    restore.mutate(
      { body: { titleId, signal: 'restored' } },
      {
        onSuccess: () => {
          qc.invalidateQueries({ queryKey: getHiddenTitlesQueryKey() })
          toast.success('Restored')
        },
        onSettled: () => markPending(titleId, false),
      },
    )
  }

  if (!user) return <Navigate to="/login" />

  return (
    <div className="mx-auto max-w-6xl px-4 py-6">
      <h1 className="text-[1.5rem] font-semibold tracking-tight text-text-primary mb-6">Hidden titles</h1>

      {isLoading && (
        <div className="grid grid-cols-3 gap-4 sm:grid-cols-4 md:grid-cols-6">
          {Array.from({ length: 6 }, (_, i) => (
            <div
              key={i}
              className="rounded-md bg-smoke/40 animate-skeleton"
              style={{ aspectRatio: '2/3' }}
            />
          ))}
        </div>
      )}

      {!isLoading && (!hidden || hidden.length === 0) && (
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <p className="text-text-secondary text-[0.9375rem]">Nothing hidden.</p>
        </div>
      )}

      {!isLoading && hidden && hidden.length > 0 && (
        <div className="grid grid-cols-3 gap-4 sm:grid-cols-4 md:grid-cols-6">
          {hidden.map((t) => (
            <div key={t.titleId} className="w-full">
              <Link to="/library/$titleId" params={{ titleId: t.titleId }} className="group block">
                <Poster path={t.posterPath} alt={t.name} size="sm" aspect="2/3" />
                <p className="text-[0.75rem] font-medium text-text-secondary group-hover:text-teal-glow transition-colors mt-1.5 line-clamp-2">
                  {t.name}
                </p>
                {t.year && <p className="text-[0.6875rem] text-text-muted">{String(t.year)}</p>}
              </Link>
              <button
                onClick={() => handleRestore(t.titleId)}
                disabled={pendingIds.has(t.titleId)}
                className={cn(
                  'mt-1.5 rounded px-2 py-1 text-[0.6875rem] font-medium bg-smoke text-text-secondary hover:bg-slate hover:text-text-primary transition-colors active:scale-[0.97]',
                  pendingIds.has(t.titleId) && 'opacity-60 pointer-events-none',
                )}
              >
                Restore
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
