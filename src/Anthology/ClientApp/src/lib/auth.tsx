import { createContext, useContext, useState, useEffect, type ReactNode } from 'react'
import { getMe, login as loginSdk, register as registerSdk, logout as logoutSdk } from '../generated/sdk.gen'
import type { AuthResponse } from '../generated/types.gen'

type AuthUser = AuthResponse

interface AuthContextType {
  user: AuthUser | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextType | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getMe()
      .then(({ data }) => {
        if (data) setUser(data)
      })
      .catch(() => { })
      .finally(() => setLoading(false))
  }, [])

  const login = async (email: string, password: string) => {
    const { data, error } = await loginSdk({ body: { email, password } })
    if (error) throw new Error('Invalid credentials')
    setUser(data!)
  }

  const register = async (email: string, password: string) => {
    const { error } = await registerSdk({ body: { email, password } })
    console.log(error)
    if (error) {
      const problem = error as Record<string, unknown>
      const validationErrors = problem.errors as Record<string, string[]> | undefined
      if (validationErrors) {
        const firstMessage = Object.values(validationErrors).flat()[0]
        if (firstMessage) throw new Error(firstMessage)
      }
      throw new Error((problem.title as string) || 'Registration failed')
    }
    await login(email, password)
  }

  const logout = async () => {
    await logoutSdk()
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
