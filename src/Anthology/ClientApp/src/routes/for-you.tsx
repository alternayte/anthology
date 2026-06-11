import { createFileRoute, Navigate, Link } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../lib/auth'
import {
  getForYouOptions,
  getForYouQueryKey,
  submitFeedbackMutation,
} from '../generated/@tanstack/react-query.gen'
import type { FeedRowDto } from '../generated/types.gen'
import { Poster } from '../components/poster'
import { cn, getErrorMessage } from '@/lib/utils'
import { toast } from 'sonner'

export const Route = createFileRoute('/for-you')({
  component: ForYouPage,
})

function ForYouPage() {
  const { user } = useAuth()
  const qc = useQueryClient()

  const onError = (error: unknown) => toast.error(getErrorMessage(error))

  const { data: rows, isLoading } = useQuery({
    ...getForYouOptions(),
    enabled: !!user,
  })

  const feedback = useMutation({
    ...submitFeedbackMutation(),
    onError,
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: getForYouQueryKey() })

  const handleHide = (titleId: string) => {
    feedback.mutate(
      { body: { titleId, signal: 'hidden' } },
      {
        onSuccess: () => {
          invalidate()
          toast.success('Hidden', {
            action: {
              label: 'Undo',
              onClick: () =>
                feedback.mutate(
                  { body: { titleId, signal: 'restored' } },
                  { onSuccess: invalidate },
                ),
            },
          })
        },
      },
    )
  }

  const handleSeen = (titleId: string) => {
    feedback.mutate(
      { body: { titleId, signal: 'seen' } },
      {
        onSuccess: () => {
          invalidate()
          toast.success('Marked as seen')
        },
      },
    )
  }

  const handleMore = (titleId: string) => {
    feedback.mutate(
      { body: { titleId, signal: 'more_like_this' } },
      {
        onSuccess: () => {
          invalidate()
          toast.success('More like this')
        },
      },
    )
  }

  if (!user) return <Navigate to="/login" />

  return (
    <div className="mx-auto max-w-6xl px-4 py-6">
      <h1 className="text-[1.5rem] font-semibold tracking-tight text-text-primary mb-6">For You</h1>

      {isLoading && (
        <div className="flex flex-col gap-10">
          {Array.from({ length: 3 }, (_, i) => (
            <div key={i}>
              <div className="h-5 w-48 rounded bg-abyss animate-skeleton mb-4" />
              <div className="flex gap-3 overflow-hidden">
                {Array.from({ length: 6 }, (_, j) => (
                  <div
                    key={j}
                    className="shrink-0 w-[120px] rounded-md bg-smoke/40 animate-skeleton"
                    style={{ aspectRatio: '2/3' }}
                  />
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {!isLoading && (!rows || rows.length === 0) && (
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <p className="text-text-secondary text-[0.9375rem]">
            Rate a few films you love to unlock recommendations.
          </p>
          <Link
            to="/search"
            className="text-teal text-[0.8125rem] mt-2 hover:text-teal-glow transition-colors"
          >
            Find something to rate
          </Link>
        </div>
      )}

      {!isLoading && rows && rows.length > 0 && (
        <div className="flex flex-col gap-10">
          {rows.map((row) => (
            <FeedRow
              key={row.seedTitleId}
              row={row}
              onHide={handleHide}
              onSeen={handleSeen}
              onMore={handleMore}
              pending={feedback.isPending}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function FeedRow({
  row,
  onHide,
  onSeen,
  onMore,
  pending,
}: {
  row: FeedRowDto
  onHide: (titleId: string) => void
  onSeen: (titleId: string) => void
  onMore: (titleId: string) => void
  pending: boolean
}) {
  if (!row.items?.length) return null

  const heading =
    row.seedName === 'Popular right now'
      ? 'Popular right now'
      : `Because you loved ${row.seedName}`

  return (
    <div>
      <h2 className="text-[0.9375rem] font-semibold text-text-primary mb-4">{heading}</h2>
      <div className="flex gap-3 overflow-x-auto pb-2 -mx-1 px-1 scrollbar-thin fade-edges-x">
        {row.items.map((t) => (
          <div key={t.titleId} className="shrink-0 w-[120px]">
            <Link
              to="/library/$titleId"
              params={{ titleId: t.titleId }}
              className="group block"
            >
              <Poster
                path={t.posterPath}
                alt={t.name}
                size="sm"
                aspect="2/3"
                viewTransitionName={`poster-${t.titleId}`}
              />
              <p className="text-[0.75rem] font-medium text-text-secondary group-hover:text-teal-glow transition-colors mt-1.5 line-clamp-2">
                {t.name}
              </p>
              {t.year && <p className="text-[0.6875rem] text-text-muted">{String(t.year)}</p>}
            </Link>
            <div className="flex items-center gap-1 mt-1.5">
              <FeedbackButton label="Hide" onClick={() => onHide(t.titleId)} disabled={pending} />
              <FeedbackButton label="Seen" onClick={() => onSeen(t.titleId)} disabled={pending} />
              <FeedbackButton label="More" onClick={() => onMore(t.titleId)} disabled={pending} />
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

function FeedbackButton({
  label,
  onClick,
  disabled,
}: {
  label: string
  onClick: () => void
  disabled: boolean
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'rounded px-1.5 py-0.5 text-[0.625rem] font-medium text-text-muted hover:text-text-primary hover:bg-smoke transition-colors active:scale-[0.97]',
        disabled && 'opacity-60 pointer-events-none',
      )}
    >
      {label}
    </button>
  )
}
