import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../lib/auth'

export const Route = createFileRoute('/library/$titleId')({
  component: ItemDetailPage,
})

function ItemDetailPage() {
  const { titleId } = Route.useParams()
  const { user } = useAuth()
  const qc = useQueryClient()

  if (!user) return <Navigate to="/login" />

  const { data: title } = useQuery({
    queryKey: ['title', titleId],
    queryFn: async () => {
      const res = await fetch(`/api/catalog/titles/${titleId}`)
      return res.ok ? res.json() : null
    },
  })

  const action = (path: string, body?: object) =>
    fetch(`/api/tracking/items/${titleId}/${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: body ? JSON.stringify(body) : undefined,
    }).then(r => r.json())

  const wantMutation = useMutation({ mutationFn: () => action('want'), onSuccess: () => qc.invalidateQueries({ queryKey: ['library'] }) })
  const startMutation = useMutation({ mutationFn: () => action('start'), onSuccess: () => qc.invalidateQueries({ queryKey: ['library'] }) })
  const finishMutation = useMutation({ mutationFn: (rating?: number) => action('finish', { rating }), onSuccess: () => qc.invalidateQueries({ queryKey: ['library'] }) })
  const abandonMutation = useMutation({ mutationFn: () => action('abandon'), onSuccess: () => qc.invalidateQueries({ queryKey: ['library'] }) })

  return (
    <div className="max-w-2xl">
      <div className="border rounded-lg bg-white p-6">
        <h1 className="text-xl font-semibold">{title?.name || 'Loading...'}</h1>
        {title?.year && <p className="text-sm text-zinc-500 mt-1">{title.year}</p>}
        {title?.overview && <p className="text-sm text-zinc-600 mt-3">{title.overview}</p>}
        <div className="flex gap-2 flex-wrap mt-4">
          <button onClick={() => wantMutation.mutate()} className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-zinc-800">Want to watch</button>
          <button onClick={() => startMutation.mutate()} className="rounded-md border px-3 py-1.5 text-sm hover:bg-zinc-50">Start watching</button>
          <button onClick={() => finishMutation.mutate(undefined)} className="rounded-md border px-3 py-1.5 text-sm hover:bg-zinc-50">Finish</button>
          <button onClick={() => abandonMutation.mutate()} className="rounded-md border px-3 py-1.5 text-sm hover:bg-zinc-50">Abandon</button>
        </div>
      </div>
    </div>
  )
}
