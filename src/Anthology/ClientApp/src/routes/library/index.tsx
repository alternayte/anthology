import { createFileRoute, Link, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../../lib/auth'
import { useState } from 'react'

export const Route = createFileRoute('/library/')({
  component: LibraryPage,
})

const statusColors: Record<string, string> = {
  want_to_consume: 'bg-blue-100 text-blue-800',
  in_progress: 'bg-yellow-100 text-yellow-800',
  finished: 'bg-green-100 text-green-800',
  abandoned: 'bg-zinc-100 text-zinc-600',
}

function LibraryPage() {
  const { user } = useAuth()
  const [sort, setSort] = useState('added')
  const [statusFilter, setStatusFilter] = useState<string>('')
  const [cursor, setCursor] = useState<string | undefined>()

  if (!user) return <Navigate to="/login" />

  const { data, isLoading } = useQuery({
    queryKey: ['library', sort, statusFilter, cursor],
    queryFn: async () => {
      const params = new URLSearchParams({ sort, dir: 'desc', size: '20' })
      if (statusFilter) params.set('status', statusFilter)
      if (cursor) params.set('cursor', cursor)
      const res = await fetch(`/api/tracking/library?${params}`, { credentials: 'include' })
      return res.json()
    },
  })

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-zinc-900">Library</h1>
        <div className="flex gap-2">
          <select value={statusFilter} onChange={e => { setStatusFilter(e.target.value); setCursor(undefined) }}
            className="rounded-md border px-3 py-2 text-sm">
            <option value="">All statuses</option>
            <option value="WantToConsume">Want to watch</option>
            <option value="InProgress">Watching</option>
            <option value="Finished">Finished</option>
            <option value="Abandoned">Abandoned</option>
          </select>
          <select value={sort} onChange={e => { setSort(e.target.value); setCursor(undefined) }}
            className="rounded-md border px-3 py-2 text-sm">
            <option value="added">Date added</option>
            <option value="rating">Rating</option>
            <option value="title">Title</option>
          </select>
        </div>
      </div>

      {isLoading && <p className="text-zinc-500">Loading...</p>}
      {data?.items?.length === 0 && (
        <p className="text-zinc-500">Your library is empty. <Link to="/search" className="text-blue-600 hover:underline">Search for a film to add.</Link></p>
      )}

      <div className="grid gap-3">
        {data?.items?.map((item: any) => (
          <Link key={item.titleId} to="/library/$titleId" params={{ titleId: item.titleId }}
            className="border rounded-lg bg-white p-4 hover:shadow-md transition-shadow cursor-pointer flex items-center justify-between">
            <div>
              <p className="font-medium text-zinc-900">{item.title}</p>
              <p className="text-sm text-zinc-500">{item.mediaType}</p>
            </div>
            <div className="flex items-center gap-3">
              {item.rating && <span className="text-sm font-medium text-zinc-700">{item.rating}/10</span>}
              <span className={`text-xs rounded px-2 py-1 ${statusColors[item.status] || 'bg-zinc-100'}`}>{item.status.replace(/_/g, ' ')}</span>
            </div>
          </Link>
        ))}
      </div>

      {data?.nextCursor && (
        <div className="mt-4 flex justify-center">
          <button onClick={() => setCursor(data.nextCursor)} className="rounded-md border px-4 py-2 text-sm hover:bg-zinc-50">Load more</button>
        </div>
      )}
    </div>
  )
}
