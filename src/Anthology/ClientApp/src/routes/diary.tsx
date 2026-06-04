import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../lib/auth'
import { useState } from 'react'

export const Route = createFileRoute('/diary')({
  component: DiaryPage,
})

function DiaryPage() {
  const { user } = useAuth()
  const [cursor, setCursor] = useState<string | undefined>()

  if (!user) return <Navigate to="/login" />

  const { data, isLoading } = useQuery({
    queryKey: ['diary', cursor],
    queryFn: async () => {
      const params = new URLSearchParams({ size: '20' })
      if (cursor) params.set('cursor', cursor)
      const res = await fetch(`/api/tracking/diary?${params}`, { credentials: 'include' })
      return res.json()
    },
  })

  return (
    <div>
      <h1 className="text-2xl font-semibold text-zinc-900 mb-6">Diary</h1>
      {isLoading && <p className="text-zinc-500">Loading...</p>}
      {data?.items?.length === 0 && <p className="text-zinc-500">No diary entries yet.</p>}
      <div className="grid gap-3">
        {data?.items?.map((entry: any, i: number) => (
          <div key={i} className="border rounded-lg bg-white p-4 flex items-center justify-between">
            <p className="text-sm text-zinc-500">{new Date(entry.occurredAt).toLocaleDateString()}</p>
            <div className="flex items-center gap-3">
              {entry.rating && <span className="text-sm font-medium">{entry.rating}/10</span>}
              <span className="text-xs border rounded px-2 py-1">{entry.status.replace(/_/g, ' ')}</span>
            </div>
          </div>
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
