import React from 'react'
import ReactDOM from 'react-dom/client'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { QueryClientProvider } from '@tanstack/react-query'
import { MotionConfig } from 'motion/react'
import { routeTree } from './routeTree.gen'
import { queryClient } from './lib/query-client'
import { AuthProvider } from './lib/auth'
import { client } from './generated/client.gen'
import './index.css'

client.setConfig({
  baseUrl: '',
  fetch: (request) => fetch(new Request(request, { credentials: 'include' })),
})

const router = createRouter({ routeTree, defaultViewTransition: true })

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <MotionConfig reducedMotion="user">
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </QueryClientProvider>
    </MotionConfig>
  </React.StrictMode>,
)
