import { useState } from 'react'
import { Box } from '@mui/material'
import { Outlet, useParams, useLocation } from 'react-router-dom'
import SidePanel from './SidePanel'
import ErrorBoundary from './ErrorBoundary'

export default function AppShell() {
  const stored = localStorage.getItem('adnd-sidebar')
  const [collapsed, setCollapsed] = useState(stored === 'true')
  const params = useParams<{ id?: string }>()
  const location = useLocation()

  const path = location.pathname

  // Only /game/:id and /admin/:id carry a game id in `:id`. Every other route in this layout
  // must resolve to no section at all — a stray `:id` from some other route would make the
  // sidebar fetch a game by the wrong id and render game links pointing at it. Character
  // routes are nested under /game/:id precisely so they land in the game section here.
  const section: 'game' | 'admin' | undefined =
    path.startsWith('/admin/') ? 'admin'
      : path.startsWith('/game/') ? 'game'
        : undefined

  const gameId = section ? params.id : undefined

  const toggle = () => {
    setCollapsed(prev => {
      const next = !prev
      localStorage.setItem('adnd-sidebar', String(next))
      return next
    })
  }

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <SidePanel collapsed={collapsed} onToggle={toggle} gameId={gameId} section={section} />
      <Box sx={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
        {/* Keyed on the route so navigating away clears a previous page's error. */}
        <ErrorBoundary key={path}>
          <Outlet />
        </ErrorBoundary>
      </Box>
    </Box>
  )
}
