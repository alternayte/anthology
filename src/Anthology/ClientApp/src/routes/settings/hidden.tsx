import { createFileRoute, Navigate } from '@tanstack/react-router'
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

  const { data: hidden, isLoading } = useQuery({
    ...getHiddenTitlesOptions(),
    enabled: !!user,
  })

  const onError = (error: unknown) => toast.error(getErrorMessage(error))

  const restore = useMutation({
    ...submitFeedbackMutation(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: getHiddenTitlesQueryKey() })
      toast.success('Restored')
    },
    onError,
  })

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
              <Poster path={t.posterPath} alt={t.name} size="sm" aspect="2/3" />
              <p className="text-[0.75rem] font-medium text-text-secondary mt-1.5 line-clamp-2">
                {t.name}
              </p>
              {t.year && <p className="text-[0.6875rem] text-text-muted">{String(t.year)}</p>}
              <button
                onClick={() => restore.mutate({ body: { titleId: t.titleId, signal: 'restored' } })}
                disabled={restore.isPending}
                className={cn(
                  'mt-1.5 rounded px-2 py-1 text-[0.6875rem] font-medium bg-smoke text-text-secondary hover:bg-slate hover:text-text-primary transition-colors active:scale-[0.97]',
                  restore.isPending && 'opacity-60 pointer-events-none',
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
