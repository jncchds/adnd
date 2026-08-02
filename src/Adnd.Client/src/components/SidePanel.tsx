import type { ReactNode } from 'react'
import { Box, Tooltip, Divider, Typography, useTheme } from '@mui/material'
import { alpha } from '@mui/material/styles'
import {
  Casino as DiceIcon,
  Tune as PresetsIcon,
  Extension as SystemsIcon,
  Settings as SettingsIcon,
  Chat as ChatIcon,
  AdminPanelSettings as AdminIcon,
  People as CharactersIcon,
  Brightness4 as DarkIcon,
  Brightness7 as LightIcon,
  Menu as MenuIcon,
  Logout as LogoutIcon,
  ArrowBack as BackIcon,
  Dashboard as OverviewIcon,
  AccountTree as PlotIcon,
  TheaterComedy as NPCIcon,
  FactCheck as ConsistencyIcon,
  Description as LogsIcon,
  SmartToy as AgentIcon,
  Home as HomeIcon,
  Login as LoginIcon,
  PersonAdd as RegisterIcon,
  NewReleases as ReleaseNotesIcon,
} from '@mui/icons-material'
import { useNavigate, useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { useColorMode } from '../main'
import { useGame } from '../api/hooks/useGame'

const SIDEBAR_W_EXPANDED = 220
const SIDEBAR_W_COLLAPSED = 56

interface NavItem {
  label: string
  icon: ReactNode
  path: string
  /**
   * Marks the item active for a whole subtree rather than one exact path. Needed for
   * Characters, which owns /game/:id/characters, the creation wizard and every sheet —
   * without it those pages render the game nav with nothing highlighted.
   */
  activePrefix?: string
}

function isActive(item: NavItem, pathname: string) {
  return item.activePrefix ? pathname.startsWith(item.activePrefix) : pathname === item.path
}

interface Props {
  collapsed: boolean
  onToggle: () => void
  gameId?: string
  /** Which sub-navigation to show. Game and Admin are separate sections. */
  section?: 'game' | 'admin'
}

function NavBtn({ item, collapsed, active }: { item: NavItem; collapsed: boolean; active: boolean }) {
  const navigate = useNavigate()
  const theme = useTheme()
  return (
    <Tooltip title={collapsed ? item.label : ''} placement="right">
      <Box
        onClick={() => navigate(item.path)}
        sx={{
          display: 'flex', alignItems: 'center', gap: 1.5,
          px: 1.5, py: 1, cursor: 'pointer', borderRadius: 1,
          bgcolor: active ? 'primary.main' : 'transparent',
          color: active ? '#fff' : theme.palette.text.secondary,
          '&:hover': { bgcolor: active ? 'primary.dark' : 'action.hover', color: active ? '#fff' : 'text.primary' },
          transition: 'all 0.15s',
          minHeight: 36,
          overflow: 'hidden',
          whiteSpace: 'nowrap',
        }}
      >
        <Box sx={{ fontSize: 20, flexShrink: 0, display: 'flex', alignItems: 'center' }}>{item.icon}</Box>
        {!collapsed && <Typography variant="body2" fontWeight={active ? 600 : 400}>{item.label}</Typography>}
      </Box>
    </Tooltip>
  )
}

export default function SidePanel({ collapsed, onToggle, gameId, section }: Props) {
  const { user, loading, logout } = useAuth()
  const { mode, toggleColorMode } = useColorMode()
  const location = useLocation()
  const theme = useTheme()
  const { game } = useGame(gameId ?? null)

  const isCreator = !!game && !!user && game.creatorId === user.id
  const inAdmin = section === 'admin' && !!gameId

  const signedInNav: NavItem[] = [
    { label: 'Games', icon: <DiceIcon fontSize="small" />, path: '/dashboard' },
    { label: 'LLM Presets', icon: <PresetsIcon fontSize="small" />, path: '/llm-presets' },
    { label: 'Systems', icon: <SystemsIcon fontSize="small" />, path: '/systems' },
    { label: 'Release Notes', icon: <ReleaseNotesIcon fontSize="small" />, path: '/release-notes' },
    { label: 'Settings', icon: <SettingsIcon fontSize="small" />, path: '/user-settings' },
  ]

  // The signed-out navigation. /release-notes is the one page both states share, which is why
  // it is a route of its own rather than a section of the landing page.
  const publicNav: NavItem[] = [
    { label: 'Home', icon: <HomeIcon fontSize="small" />, path: '/' },
    { label: 'Sign In', icon: <LoginIcon fontSize="small" />, path: '/login' },
    { label: 'Register', icon: <RegisterIcon fontSize="small" />, path: '/register' },
    { label: 'Release Notes', icon: <ReleaseNotesIcon fontSize="small" />, path: '/release-notes' },
  ]

  // While the session is still being restored we know neither nav is right, and rendering the
  // signed-out one would flash "Sign In" at a returning player on every reload.
  const mainNav: NavItem[] = loading ? [] : user ? signedInNav : publicNav

  const gameNav: NavItem[] = gameId ? [
    { label: 'Chat', icon: <ChatIcon fontSize="small" />, path: `/game/${gameId}` },
    {
      label: 'Characters', icon: <CharactersIcon fontSize="small" />,
      path: `/game/${gameId}/characters`,
      // Also covers /character/new and /character/:characterId under this game.
      activePrefix: `/game/${gameId}/character`,
    },
    ...(isCreator ? [{ label: 'Admin', icon: <AdminIcon fontSize="small" />, path: `/admin/${gameId}` }] : []),
  ] : []

  // Every Game Admin page. These all had routes but no navigation — the only way to
  // reach them was to type the URL.
  const adminNav: NavItem[] = gameId ? [
    { label: 'Overview', icon: <OverviewIcon fontSize="small" />, path: `/admin/${gameId}` },
    { label: 'Plot Board', icon: <PlotIcon fontSize="small" />, path: `/admin/${gameId}/plot-board` },
    { label: 'NPCs', icon: <NPCIcon fontSize="small" />, path: `/admin/${gameId}/npcs` },
    { label: 'Characters', icon: <CharactersIcon fontSize="small" />, path: `/admin/${gameId}/characters` },
    { label: 'Consistency', icon: <ConsistencyIcon fontSize="small" />, path: `/admin/${gameId}/consistency` },
    { label: 'LLM Logs', icon: <LogsIcon fontSize="small" />, path: `/admin/${gameId}/llm-logs` },
    { label: 'Agent Calls', icon: <AgentIcon fontSize="small" />, path: `/admin/${gameId}/agent-calls` },
    { label: 'Game Settings', icon: <SettingsIcon fontSize="small" />, path: `/admin/${gameId}/settings` },
  ] : []

  const sectionNav = inAdmin ? adminNav : gameNav

  const sidebarBg = theme.palette.mode === 'dark' ? '#0e0b14' : '#f0ebff'

  return (
    <Box
      sx={{
        width: collapsed ? SIDEBAR_W_COLLAPSED : SIDEBAR_W_EXPANDED,
        minHeight: '100vh',
        bgcolor: sidebarBg,
        borderRight: `1px solid ${theme.palette.divider}`,
        display: 'flex', flexDirection: 'column',
        transition: 'width 0.2s ease',
        flexShrink: 0,
        overflow: 'hidden',
      }}
    >
      {/* Header — the whole row is the collapse/expand toggle, not just an icon in the corner */}
      <Tooltip title={collapsed ? 'Expand sidebar' : ''} placement="right">
        <Box
          component="button"
          onClick={onToggle}
          sx={{
            display: 'flex', alignItems: 'center', gap: 1,
            width: '100%', border: 0, background: 'none', cursor: 'pointer',
            p: 1, color: theme.palette.text.primary, font: 'inherit',
            justifyContent: collapsed ? 'center' : 'flex-start',
            '&:hover': { bgcolor: 'action.hover' },
          }}
        >
          <MenuIcon fontSize="small" />
          {!collapsed && (
            <>
              <Typography variant="body2" fontWeight={700} sx={{ letterSpacing: 1 }}>
                ADnD
              </Typography>
              <Box
                component="a"
                href="https://github.com/jncchds/adnd"
                target="_blank"
                rel="noopener noreferrer"
                onClick={e => e.stopPropagation()}
                sx={{
                  fontSize: 10, lineHeight: 1.4, textDecoration: 'none',
                  color: theme.palette.text.secondary,
                  bgcolor: alpha(theme.palette.primary.main, 0.15),
                  border: `1px solid ${alpha(theme.palette.primary.main, 0.25)}`,
                  px: 0.75, py: 0.1, borderRadius: 10,
                  '&:hover': { bgcolor: alpha(theme.palette.primary.main, 0.25) },
                }}
              >
                v{__APP_VERSION__}
              </Box>
              <Box component="span" sx={{ fontSize: 9, fontWeight: 700, bgcolor: 'error.main', color: '#fff', px: 0.5, py: 0.1, borderRadius: 0.5 }}>
                ALPHA
              </Box>
            </>
          )}
        </Box>
      </Tooltip>

      <Divider />

      {/* Main nav */}
      <Box sx={{ flex: 1, py: 1, px: 0.5, display: 'flex', flexDirection: 'column', gap: 0.25, overflowY: 'auto' }}>
        {mainNav.map(item => (
          <NavBtn key={item.path} item={item} collapsed={collapsed} active={isActive(item, location.pathname)} />
        ))}

        {sectionNav.length > 0 && (
          <>
            <Divider sx={{ my: 0.5 }} />

            {/* Leaving admin returns to the game it belongs to, not the dashboard. */}
            {inAdmin && (
              <NavBtn
                item={{ label: 'Back to Game', icon: <BackIcon fontSize="small" />, path: `/game/${gameId}` }}
                collapsed={collapsed}
                active={false}
              />
            )}

            {sectionNav.map(item => (
              <NavBtn key={item.path} item={item} collapsed={collapsed} active={isActive(item, location.pathname)} />
            ))}
          </>
        )}
      </Box>

      <Divider />

      {/* Bottom actions */}
      <Box sx={{ py: 1, px: 0.5, display: 'flex', flexDirection: 'column', gap: 0.25 }}>
        <Tooltip title={collapsed ? (mode === 'dark' ? 'Light mode' : 'Dark mode') : ''} placement="right">
          <Box
            onClick={toggleColorMode}
            sx={{
              display: 'flex', alignItems: 'center', gap: 1.5,
              px: 1.5, py: 1, cursor: 'pointer', borderRadius: 1,
              color: 'text.secondary',
              '&:hover': { bgcolor: 'action.hover', color: 'text.primary' },
              minHeight: 36, overflow: 'hidden', whiteSpace: 'nowrap',
            }}
          >
            <Box sx={{ fontSize: 20, flexShrink: 0, display: 'flex', alignItems: 'center' }}>
              {mode === 'dark' ? <LightIcon fontSize="small" /> : <DarkIcon fontSize="small" />}
            </Box>
            {!collapsed && <Typography variant="body2">{mode === 'dark' ? 'Light mode' : 'Dark mode'}</Typography>}
          </Box>
        </Tooltip>
        {user && <Tooltip title={collapsed ? 'Logout' : ''} placement="right">
          <Box
            onClick={logout}
            sx={{
              display: 'flex', alignItems: 'center', gap: 1.5,
              px: 1.5, py: 1, cursor: 'pointer', borderRadius: 1,
              color: 'text.secondary',
              '&:hover': { bgcolor: 'action.hover', color: 'error.main' },
              minHeight: 36, overflow: 'hidden', whiteSpace: 'nowrap',
            }}
          >
            <Box sx={{ fontSize: 20, flexShrink: 0, display: 'flex', alignItems: 'center' }}>
              <LogoutIcon fontSize="small" />
            </Box>
            {!collapsed && <Typography variant="body2">Logout</Typography>}
          </Box>
        </Tooltip>}
      </Box>
    </Box>
  )
}
