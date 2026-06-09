import { createRootRoute, Outlet, Link } from '@tanstack/react-router'
import { useAuth } from '../lib/auth'
import { Toaster } from '@/components/ui/sonner'

export const Route = createRootRoute({
  component: RootLayout,
})

function RootLayout() {
  const { user, loading, logout } = useAuth()

  if (loading) return <div className="flex items-center justify-center h-screen bg-void text-text-primary">Loading...</div>

  return (
    <div className="min-h-screen bg-void text-text-primary">
      <nav className="border-b border-border bg-abyss">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
          <div className="flex items-center gap-6">
            <Link to="/" className="font-semibold text-lg tracking-tight text-text-primary">Anthology</Link>
            {user && (
              <>
                <Link to="/library" className="text-[0.8125rem] font-medium text-text-secondary hover:text-text-primary transition-colors [&.active]:text-text-primary">Library</Link>
                <Link to="/diary" className="text-[0.8125rem] font-medium text-text-secondary hover:text-text-primary transition-colors [&.active]:text-text-primary">Diary</Link>
                <Link to="/search" className="text-[0.8125rem] font-medium text-text-secondary hover:text-text-primary transition-colors [&.active]:text-text-primary">Search</Link>
              </>
            )}
          </div>
          <div>
            {user ? (
              <button onClick={() => logout()} className="text-[0.8125rem] font-medium text-text-secondary hover:text-text-primary transition-colors">Sign out</button>
            ) : (
              <Link to="/login" className="text-[0.8125rem] font-medium text-text-secondary hover:text-text-primary transition-colors">Sign in</Link>
            )}
          </div>
        </div>
      </nav>
      <main className="max-w-6xl mx-auto px-4 py-6">
        <Outlet />
      </main>
      <Toaster theme="dark" position="bottom-right" richColors />
    </div>
  )
}
