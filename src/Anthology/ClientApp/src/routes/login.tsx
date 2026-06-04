import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useAuth } from '../lib/auth'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      await login(email, password)
      navigate({ to: '/library' })
    } catch {
      setError('Invalid credentials')
    }
  }

  return (
    <div className="flex justify-center pt-20">
      <div className="w-full max-w-sm border rounded-lg bg-white p-6 shadow-sm">
        <h2 className="text-xl font-semibold mb-4">Sign in</h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="email" className="text-sm font-medium">Email</label>
            <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required
              className="w-full rounded-md border px-3 py-2 text-sm" />
          </div>
          <div className="space-y-2">
            <label htmlFor="password" className="text-sm font-medium">Password</label>
            <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required
              className="w-full rounded-md border px-3 py-2 text-sm" />
          </div>
          {error && <p className="text-sm text-red-600">{error}</p>}
          <button type="submit" className="w-full rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-800">Sign in</button>
        </form>
        <p className="mt-4 text-sm text-zinc-500">No account? <Link to="/register" className="text-blue-600 hover:underline">Create one</Link></p>
      </div>
    </div>
  )
}
