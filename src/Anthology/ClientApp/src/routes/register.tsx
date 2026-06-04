import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useAuth } from '../lib/auth'

export const Route = createFileRoute('/register')({
  component: RegisterPage,
})

function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      await register(email, password)
      navigate({ to: '/library' })
    } catch (err: any) {
      setError(err.message || 'Registration failed')
    }
  }

  return (
    <div className="flex justify-center pt-20">
      <div className="w-full max-w-sm border rounded-lg bg-white p-6 shadow-sm">
        <h2 className="text-xl font-semibold mb-4">Create account</h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="email" className="text-sm font-medium">Email</label>
            <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required
              className="w-full rounded-md border px-3 py-2 text-sm" />
          </div>
          <div className="space-y-2">
            <label htmlFor="password" className="text-sm font-medium">Password (min 8 characters)</label>
            <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required minLength={8}
              className="w-full rounded-md border px-3 py-2 text-sm" />
          </div>
          {error && <p className="text-sm text-red-600">{error}</p>}
          <button type="submit" className="w-full rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-800">Create account</button>
        </form>
        <p className="mt-4 text-sm text-zinc-500">Already have an account? <Link to="/login" className="text-blue-600 hover:underline">Sign in</Link></p>
      </div>
    </div>
  )
}
