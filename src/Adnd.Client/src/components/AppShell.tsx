import { useState } from 'react'
import { Box } from '@mui/material'
import { Outlet, useParams, useLocation } from 'react-router-dom'
import SidePanel from './SidePanel'

export default function AppShell() {
  const stored = localStorage.getItem('adnd-sidebar')
  const [collapsed, setCollapsed] = useState(stored === 'true')
  const params = useParams<{ id?: string }>()
  const location = useLocation()

  const gameId = params.id
  const path = location.pathname

  let currentView: string | undefined
  if (path.includes('/admin/')) currentView = 'Admin'
  else if (path.includes('/game/')) currentView = 'Game'

  const toggle = () => {
    setCollapsed(prev => {
      const next = !prev
      localStorage.setItem('adnd-sidebar', String(next))
      return next
    })
  }

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <SidePanel collapsed={collapsed} onToggle={toggle} gameId={gameId} currentView={currentView} />
      <Box sx={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
        <Outlet />
      </Box>
    </Box>
  )
}
