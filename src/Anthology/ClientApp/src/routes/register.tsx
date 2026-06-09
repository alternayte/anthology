import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useState } from 'react'
import { toast } from 'sonner'
import { useAuth } from '../lib/auth'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/register')({
  component: RegisterPage,
})

function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    try {
      await register(email, password)
      navigate({ to: '/library' })
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Registration failed'
      toast.error(message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex justify-center pt-20">
      <div className="w-full max-w-sm rounded-[10px] bg-abyss p-6">
        <h2 className="text-[1.25rem] font-semibold text-text-primary mb-5">Create account</h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label htmlFor="email" className="text-[0.8125rem] font-medium text-text-secondary">Email</label>
            <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required
              className="w-full rounded-md bg-smoke border border-transparent px-3 py-2 text-[0.875rem] text-text-primary placeholder:text-text-muted focus:border-teal focus:outline-none transition-colors" />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="password" className="text-[0.8125rem] font-medium text-text-secondary">Password (min 8 characters)</label>
            <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required minLength={8}
              className="w-full rounded-md bg-smoke border border-transparent px-3 py-2 text-[0.875rem] text-text-primary placeholder:text-text-muted focus:border-teal focus:outline-none transition-colors" />
          </div>
          <button type="submit" disabled={loading}
            className={cn('w-full rounded-md bg-teal px-4 py-2.5 text-[0.8125rem] font-medium text-void hover:bg-teal-glow transition-colors', loading && 'opacity-50 pointer-events-none')}>
            Create account
          </button>
        </form>
        <p className="mt-4 text-[0.8125rem] text-text-muted">Already have an account? <Link to="/login" className="text-teal hover:text-teal-glow transition-colors">Sign in</Link></p>
      </div>
    </div>
  )
}
