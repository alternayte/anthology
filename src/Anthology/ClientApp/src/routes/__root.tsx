import { createRootRoute, Outlet, Link } from '@tanstack/react-router'
import { useAuth } from '../lib/auth'

export const Route = createRootRoute({
  component: RootLayout,
})

function RootLayout() {
  const { user, loading, logout } = useAuth()

  if (loading) return <div className="flex items-center justify-center h-screen">Loading...</div>

  return (
    <div className="min-h-screen bg-zinc-50">
      <nav className="border-b bg-white">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
          <div className="flex items-center gap-6">
            <Link to="/" className="font-semibold text-lg text-zinc-900">Anthology</Link>
            {user && (
              <>
                <Link to="/library" className="text-sm text-zinc-600 hover:text-zinc-900 [&.active]:text-zinc-900 [&.active]:font-medium">Library</Link>
                <Link to="/diary" className="text-sm text-zinc-600 hover:text-zinc-900 [&.active]:text-zinc-900 [&.active]:font-medium">Diary</Link>
                <Link to="/search" className="text-sm text-zinc-600 hover:text-zinc-900 [&.active]:text-zinc-900 [&.active]:font-medium">Search</Link>
              </>
            )}
          </div>
          <div>
            {user ? (
              <button onClick={() => logout()} className="text-sm text-zinc-600 hover:text-zinc-900">Sign out</button>
            ) : (
              <Link to="/login" className="text-sm text-zinc-600 hover:text-zinc-900">Sign in</Link>
            )}
          </div>
        </div>
      </nav>
      <main className="max-w-5xl mx-auto px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
