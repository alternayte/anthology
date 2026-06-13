import { createRootRoute, Outlet, Link, useRouterState } from '@tanstack/react-router'
import { motion } from 'motion/react'
import { useAuth } from '../lib/auth'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'

export const Route = createRootRoute({
  component: RootLayout,
})

function NavLink({ to, label, active }: { to: '/for-you' | '/library' | '/diary' | '/search'; label: string; active: boolean }) {
  return (
    <Link
      to={to}
      className={cn(
        'relative flex h-14 items-center text-[0.8125rem] font-medium transition-colors',
        active ? 'text-text-primary' : 'text-text-secondary hover:text-text-primary',
      )}
    >
      {label}
      {active && (
        <motion.span
          layoutId="nav-active"
          className="absolute inset-x-0 bottom-0 h-0.5 rounded-full bg-teal"
          transition={{ duration: 0.25, ease: [0.22, 1, 0.36, 1] }}
        />
      )}
    </Link>
  )
}

function RootLayout() {
  const { user, loading, logout } = useAuth()
  const pathname = useRouterState({ select: (s) => s.location.pathname })

  if (loading) {
    return (
      <div className="flex h-screen items-center justify-center bg-void">
        <div className="h-5 w-5 animate-spin rounded-full border-2 border-ash border-t-teal" />
      </div>
    )
  }

  return (
    <TooltipProvider>
    <div className="min-h-screen bg-void text-text-primary">
      <nav className="sticky top-0 z-40 border-b border-border bg-void/75 backdrop-blur-md">
        <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
          <div className="flex items-center gap-6">
            <Link to="/" className="text-lg font-semibold tracking-tight text-text-primary">Anthology</Link>
            {user && (
              <>
                <NavLink to="/for-you" label="For You" active={pathname.startsWith('/for-you')} />
                <NavLink to="/library" label="Library" active={pathname.startsWith('/library')} />
                <NavLink to="/diary" label="Diary" active={pathname.startsWith('/diary')} />
                <NavLink to="/search" label="Search" active={pathname.startsWith('/search')} />
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
      <main>
        <Outlet />
      </main>
      <Toaster theme="dark" position="bottom-right" richColors />
    </div>
    </TooltipProvider>
  )
}
