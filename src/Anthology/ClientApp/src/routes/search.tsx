import { createFileRoute, Navigate, useNavigate } from '@tanstack/react-router'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useAuth } from '../lib/auth'
import { useState } from 'react'
import { searchCatalogOptions, addTitleMutation } from '../generated/@tanstack/react-query.gen'
import type { TitleSearchResult } from '../generated/types.gen'

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
    ...searchCatalogOptions({ query: { term: search } }),
    enabled: search.length > 0,
  })

  const addMutation = useMutation({
    ...addTitleMutation(),
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
        {results?.map((r: TitleSearchResult) => (
          <div key={r.tmdbId} onClick={() => addMutation.mutate({ body: { tmdbId: r.tmdbId } })}
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
