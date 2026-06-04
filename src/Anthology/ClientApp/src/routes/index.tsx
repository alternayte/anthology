import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useAuth } from '../lib/auth'

export const Route = createFileRoute('/')({
  component: IndexPage,
})

function IndexPage() {
  const { user } = useAuth()
  return user ? <Navigate to="/library" /> : <Navigate to="/login" />
}
