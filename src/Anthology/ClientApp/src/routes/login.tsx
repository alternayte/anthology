import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useState } from 'react'
import { toast } from 'sonner'
import { motion } from 'motion/react'
import { useAuth } from '../lib/auth'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    try {
      await login(email, password)
      navigate({ to: '/library' })
    } catch {
      toast.error('Invalid credentials')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="mx-auto max-w-6xl px-4 flex justify-center pt-20">
      <motion.div
        className="w-full max-w-sm rounded-[10px] bg-abyss p-6"
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
      >
        <h2 className="text-[1.25rem] font-semibold text-text-primary mb-5">Sign in</h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label htmlFor="email" className="text-[0.8125rem] font-medium text-text-secondary">Email</label>
            <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required
              className="w-full rounded-md bg-smoke border border-transparent px-3 py-2 text-[0.875rem] text-text-primary placeholder:text-text-muted focus:border-teal focus:shadow-[var(--shadow-glow)] focus:outline-none transition-[border-color,box-shadow] duration-150" />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="password" className="text-[0.8125rem] font-medium text-text-secondary">Password</label>
            <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required
              className="w-full rounded-md bg-smoke border border-transparent px-3 py-2 text-[0.875rem] text-text-primary placeholder:text-text-muted focus:border-teal focus:shadow-[var(--shadow-glow)] focus:outline-none transition-[border-color,box-shadow] duration-150" />
          </div>
          <button type="submit" disabled={loading}
            className={cn('w-full inline-flex items-center justify-center gap-2 rounded-md bg-teal px-4 py-2.5 text-[0.8125rem] font-medium text-void hover:bg-teal-glow transition-all duration-150 active:scale-[0.98]', loading && 'opacity-70 pointer-events-none')}>
            {loading && <span className="h-3.5 w-3.5 animate-spin rounded-full border-[1.5px] border-current border-t-transparent" />}
            Sign in
          </button>
        </form>
        <p className="mt-4 text-[0.8125rem] text-text-muted">No account? <Link to="/register" className="text-teal hover:text-teal-glow transition-colors">Create one</Link></p>
      </motion.div>
    </div>
  )
}
