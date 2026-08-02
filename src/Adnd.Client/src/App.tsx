import { BrowserRouter, Routes, Route, Navigate, Outlet, useParams } from 'react-router-dom'
import { CircularProgress, Box } from '@mui/material'
import { AuthProvider, useAuth } from './context/AuthContext'
import { useGame } from './api/hooks/useGame'
import AppShell from './components/AppShell'

import AuthPage from './pages/AuthPage'
import LandingPage from './pages/LandingPage'
import ReleaseNotesPage from './pages/ReleaseNotesPage'
import DashboardPage from './pages/DashboardPage'
import GameChatPage from './pages/GameChatPage'
import GameSettingsPage from './pages/GameSettingsPage'
import AdminDashboardPage from './pages/AdminDashboardPage'
import AdminPlotBoardPage from './pages/AdminPlotBoardPage'
import AdminNPCsPage from './pages/AdminNPCsPage'
import AdminCharactersPage from './pages/AdminCharactersPage'
import AdminConsistencyPage from './pages/AdminConsistencyPage'
import AdminLLMLogsPage from './pages/AdminLLMLogsPage'
import AdminAgentCallsPage from './pages/AdminAgentCallsPage'
import CharacterSheetPage from './pages/CharacterSheetPage'
import CharacterCreateWizard from './pages/CharacterCreateWizard'
import GameCharactersPage from './pages/GameCharactersPage'
import LLMPresetsPage from './pages/LLMPresetsPage'
import SystemsPage from './pages/SystemsPage'
import UserSettingsPage from './pages/UserSettingsPage'

function Centered({ children }: { children: React.ReactNode }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh' }}>
      {children}
    </Box>
  )
}

/** Layout guard for the signed-in half of the app. Renders the nested routes via `Outlet`. */
function RequireAuth() {
  const { user, loading } = useAuth()
  if (loading) return <Centered><CircularProgress /></Centered>
  if (!user) return <Navigate to="/login" replace />
  return <Outlet />
}

/**
 * The public pages — landing, sign in, register — are meaningless once signed in, and a
 * returning player following an old /login bookmark should land at their games, not at a form
 * asking them to sign in again.
 */
function PublicOnly({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth()
  if (loading) return <Centered><CircularProgress /></Centered>
  if (user) return <Navigate to="/dashboard" replace />
  return <>{children}</>
}

/**
 * Guards the Game Admin section. RequireAuth only checks that *someone* is signed in, so
 * any player could open the full GM console just by typing /admin/{gameId}.
 * The server enforces this too; this stops the UI from rendering a console that 403s.
 */
function RequireGameCreator({ children }: { children: React.ReactNode }) {
  const { id } = useParams<{ id: string }>()
  const { user } = useAuth()
  const { game, loading, error } = useGame(id ?? null)

  // useGame starts with loading=false and game=null, so "no game and no error yet" means
  // the fetch has not run — treat it as loading rather than redirecting on first render.
  if (loading || (!game && !error)) return <Centered><CircularProgress /></Centered>
  if (error || !game) return <Navigate to="/dashboard" replace />
  if (!user || game.creatorId !== user.id) return <Navigate to={`/game/${id}`} replace />

  return <>{children}</>
}

function AppRoutes() {
  return (
    <Routes>
      {/* One shell for both halves of the app: SidePanel switches its own navigation on the
          auth state, so a signed-out visitor gets the same chrome, theme toggle and layout as
          a player at the table. */}
      <Route element={<AppShell />}>
        <Route index element={<PublicOnly><LandingPage /></PublicOnly>} />
        <Route path="/login" element={<PublicOnly><AuthPage mode="login" /></PublicOnly>} />
        <Route path="/register" element={<PublicOnly><AuthPage mode="register" /></PublicOnly>} />
        {/* Public, but equally reachable from the signed-in navigation. */}
        <Route path="/release-notes" element={<ReleaseNotesPage />} />

        <Route element={<RequireAuth />}>
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/llm-presets" element={<LLMPresetsPage />} />
          <Route path="/systems" element={<SystemsPage />} />
          <Route path="/user-settings" element={<UserSettingsPage />} />
          <Route path="/game/:id" element={<GameChatPage />} />
          <Route path="/game/:id/characters" element={<GameCharactersPage />} />
          {/* A character only exists as part of a game, so its URL says which one. That also
              gives AppShell a game id to build the game navigation from — under the old
              top-level /character/:id the sheet rendered with no game nav at all. */}
          <Route path="/game/:id/character/new" element={<CharacterCreateWizard />} />
          <Route path="/game/:id/character/:characterId" element={<CharacterSheetPage />} />
          <Route path="/admin/:id" element={<RequireGameCreator><AdminDashboardPage /></RequireGameCreator>} />
          <Route path="/admin/:id/plot-board" element={<RequireGameCreator><AdminPlotBoardPage /></RequireGameCreator>} />
          <Route path="/admin/:id/npcs" element={<RequireGameCreator><AdminNPCsPage /></RequireGameCreator>} />
          <Route path="/admin/:id/characters" element={<RequireGameCreator><AdminCharactersPage /></RequireGameCreator>} />
          <Route path="/admin/:id/consistency" element={<RequireGameCreator><AdminConsistencyPage /></RequireGameCreator>} />
          <Route path="/admin/:id/llm-logs" element={<RequireGameCreator><AdminLLMLogsPage /></RequireGameCreator>} />
          <Route path="/admin/:id/agent-calls" element={<RequireGameCreator><AdminAgentCallsPage /></RequireGameCreator>} />
          <Route path="/admin/:id/settings" element={<RequireGameCreator><GameSettingsPage /></RequireGameCreator>} />
        </Route>

        {/* Unknown paths go to "/", which resolves to the dashboard for a signed-in player and
            to the landing page for everyone else — the old redirect straight to /dashboard
            bounced signed-out visitors into the sign-in form. */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  )
}
