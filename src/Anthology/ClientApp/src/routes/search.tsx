import { createFileRoute, Navigate, useNavigate } from '@tanstack/react-router'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useAuth } from '../lib/auth'
import { useState } from 'react'

export const Route = createFileRoute('/search')({
  component: SearchPage,
})

function SearchPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [term, setTerm] = useState('')
  const [search, setSearch] = useState('')

  if (!user) return <Navigate to="/login" />

  const { data: results, isLoading } = useQuery({
    queryKey: ['search', search],
    queryFn: async () => {
      if (!search) return []
      const res = await fetch(`/api/catalog/search?term=${encodeURIComponent(search)}`, { credentials: 'include' })
      return res.json()
    },
    enabled: search.length > 0,
  })

  const addTitle = useMutation({
    mutationFn: async (tmdbId: number) => {
      const res = await fetch('/api/catalog/titles', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ tmdbId }),
      })
      return res.json()
    },
    onSuccess: (data) => {
      if (data?.titleId) navigate({ to: '/library/$titleId', params: { titleId: data.titleId } })
    },
  })

  return (
    <div>
      <h1 className="text-2xl font-semibold text-zinc-900 mb-6">Search films</h1>
      <form onSubmit={e => { e.preventDefault(); setSearch(term) }} className="flex gap-2 mb-6">
        <input value={term} onChange={e => setTerm(e.target.value)} placeholder="Search TMDB..."
          className="max-w-md rounded-md border px-3 py-2 text-sm flex-1" />
        <button type="submit" className="rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-800">Search</button>
      </form>

      {isLoading && <p className="text-zinc-500">Searching...</p>}

      <div className="grid gap-3">
        {results?.map?.((r: any) => (
          <div key={r.tmdbId} onClick={() => addTitle.mutate(r.tmdbId)}
            className="cursor-pointer border rounded-lg bg-white p-4 hover:shadow-md transition-shadow flex items-center gap-4">
            {r.posterPath && <img src={r.posterPath} alt="" className="w-12 h-18 rounded object-cover" />}
            <div>
              <p className="font-medium text-zinc-900">{r.name}</p>
              <p className="text-sm text-zinc-500">{r.year}</p>
              {r.overview && <p className="text-xs text-zinc-400 mt-1 line-clamp-2">{r.overview}</p>}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
