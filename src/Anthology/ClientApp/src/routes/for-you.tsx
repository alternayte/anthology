import { createFileRoute, Navigate, Link } from '@tanstack/react-router'
import { useState } from 'react'
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
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import { Ban, Check, Sparkles, type LucideIcon } from 'lucide-react'
import { toast } from 'sonner'

export const Route = createFileRoute('/for-you')({
  component: ForYouPage,
})

function ForYouPage() {
  const { user } = useAuth()
  const qc = useQueryClient()
  const [pendingIds, setPendingIds] = useState<Set<string>>(new Set())

  const onError = (error: unknown) => toast.error(getErrorMessage(error))

  const { data: rows, isLoading } = useQuery({
    ...getForYouOptions(),
    enabled: !!user,
  })

  const feedback = useMutation({
    ...submitFeedbackMutation(),
    onError,
  })

  const markPending = (id: string, on: boolean) =>
    setPendingIds(prev => { const next = new Set(prev); on ? next.add(id) : next.delete(id); return next })

  const invalidate = () => qc.invalidateQueries({ queryKey: getForYouQueryKey() })

  const handleHide = (titleId: string) => {
    markPending(titleId, true)
    feedback.mutate(
      { body: { titleId, signal: 'hidden' } },
      {
        onSuccess: () => {
          invalidate()
          toast.success('Hidden', {
            action: {
              label: 'Undo',
              onClick: () => {
                markPending(titleId, true)
                feedback.mutate(
                  { body: { titleId, signal: 'restored' } },
                  { onSuccess: invalidate, onSettled: () => markPending(titleId, false) },
                )
              },
            },
          })
        },
        onSettled: () => markPending(titleId, false),
      },
    )
  }

  const handleSeen = (titleId: string) => {
    markPending(titleId, true)
    feedback.mutate(
      { body: { titleId, signal: 'seen' } },
      {
        onSuccess: () => {
          invalidate()
          toast.success('Marked as seen')
        },
        onSettled: () => markPending(titleId, false),
      },
    )
  }

  const handleMore = (titleId: string) => {
    markPending(titleId, true)
    feedback.mutate(
      { body: { titleId, signal: 'more_like_this' } },
      {
        onSuccess: () => {
          invalidate()
          toast.success('More like this')
        },
        onSettled: () => markPending(titleId, false),
      },
    )
  }

  if (!user) return <Navigate to="/login" />

  return (
    <div className="mx-auto max-w-6xl px-4 py-6">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-[1.5rem] font-semibold tracking-tight text-text-primary">For You</h1>
        <Link
          to="/settings/hidden"
          className="text-[0.8125rem] font-medium text-text-secondary hover:text-text-primary transition-colors"
        >
          Hidden titles
        </Link>
      </div>

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
              pendingIds={pendingIds}
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
  pendingIds,
}: {
  row: FeedRowDto
  onHide: (titleId: string) => void
  onSeen: (titleId: string) => void
  onMore: (titleId: string) => void
  pendingIds: Set<string>
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
            <div className="flex items-center gap-0.5 mt-1.5">
              <FeedbackButton
                icon={Ban}
                label="Not interested"
                description="Hide this and stop recommending it."
                onClick={() => onHide(t.titleId)}
                disabled={pendingIds.has(t.titleId)}
              />
              <FeedbackButton
                icon={Check}
                label="Seen it"
                description="You've already watched this — remove it from your feed."
                onClick={() => onSeen(t.titleId)}
                disabled={pendingIds.has(t.titleId)}
              />
              <FeedbackButton
                icon={Sparkles}
                label="More like this"
                description="Recommend more titles based on this one."
                onClick={() => onMore(t.titleId)}
                disabled={pendingIds.has(t.titleId)}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

function FeedbackButton({
  icon: Icon,
  label,
  description,
  onClick,
  disabled,
}: {
  icon: LucideIcon
  label: string
  description: string
  onClick: () => void
  disabled: boolean
}) {
  return (
    <Tooltip>
      <TooltipTrigger
        onClick={onClick}
        disabled={disabled}
        aria-label={label}
        className={cn(
          'rounded p-1 text-text-muted hover:text-text-primary hover:bg-smoke transition-colors active:scale-[0.97]',
          disabled && 'opacity-60 pointer-events-none',
        )}
      >
        <Icon className="size-3.5" strokeWidth={2} />
      </TooltipTrigger>
      <TooltipContent className="flex-col items-start gap-0.5 max-w-[180px]">
        <span className="font-semibold">{label}</span>
        <span className="text-background/70 leading-snug">{description}</span>
      </TooltipContent>
    </Tooltip>
  )
}
