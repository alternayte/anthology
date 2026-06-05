import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../lib/auth'
import { getTitleOptions, wantItemMutation, startItemMutation, finishItemMutation, abandonItemMutation } from '../../generated/@tanstack/react-query.gen'

export const Route = createFileRoute('/library/$titleId')({
  component: ItemDetailPage,
})

function ItemDetailPage() {
  const { titleId } = Route.useParams()
  const { user } = useAuth()
  const qc = useQueryClient()

  if (!user) return <Navigate to="/login" />

  const onSuccess = () => qc.invalidateQueries({ queryKey: ['library'] })

  const { data: title } = useQuery({
    ...getTitleOptions({ path: { titleId } }),
  })

  const want = useMutation({ ...wantItemMutation(), onSuccess })
  const start = useMutation({ ...startItemMutation(), onSuccess })
  const finish = useMutation({ ...finishItemMutation(), onSuccess })
  const abandon = useMutation({ ...abandonItemMutation(), onSuccess })

  return (
    <div className="max-w-2xl">
      <div className="border rounded-lg bg-white p-6">
        <h1 className="text-xl font-semibold">{title?.name || 'Loading...'}</h1>
        {title?.year && <p className="text-sm text-zinc-500 mt-1">{title.year}</p>}
        {title?.overview && <p className="text-sm text-zinc-600 mt-3">{title.overview}</p>}
        <div className="flex gap-2 flex-wrap mt-4">
          <button onClick={() => want.mutate({ path: { titleId } })} className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-zinc-800">Want to watch</button>
          <button onClick={() => start.mutate({ path: { titleId } })} className="rounded-md border px-3 py-1.5 text-sm hover:bg-zinc-50">Start watching</button>
          <button onClick={() => finish.mutate({ path: { titleId }, body: { rating: null } })} className="rounded-md border px-3 py-1.5 text-sm hover:bg-zinc-50">Finish</button>
          <button onClick={() => abandon.mutate({ path: { titleId } })} className="rounded-md border px-3 py-1.5 text-sm hover:bg-zinc-50">Abandon</button>
        </div>
      </div>
    </div>
  )
}
