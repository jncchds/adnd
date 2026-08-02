import { BrowserRouter, Routes, Route, Navigate, useParams } from 'react-router-dom'
import { CircularProgress, Box } from '@mui/material'
import { AuthProvider, useAuth } from './context/AuthContext'
import { useGame } from './api/hooks/useGame'
import AppShell from './components/AppShell'

import AuthPage from './pages/AuthPage'
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

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth()
  if (loading) return <Centered><CircularProgress /></Centered>
  if (!user) return <Navigate to="/login" replace />
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
      <Route path="/login" element={<AuthPage />} />
      <Route path="/register" element={<AuthPage />} />

      <Route element={<RequireAuth><AppShell /></RequireAuth>}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/llm-presets" element={<LLMPresetsPage />} />
        <Route path="/systems" element={<SystemsPage />} />
        <Route path="/user-settings" element={<UserSettingsPage />} />
        <Route path="/game/:id" element={<GameChatPage />} />
        <Route path="/game/:id/characters" element={<GameCharactersPage />} />
        <Route path="/admin/:id" element={<RequireGameCreator><AdminDashboardPage /></RequireGameCreator>} />
        <Route path="/admin/:id/plot-board" element={<RequireGameCreator><AdminPlotBoardPage /></RequireGameCreator>} />
        <Route path="/admin/:id/npcs" element={<RequireGameCreator><AdminNPCsPage /></RequireGameCreator>} />
        <Route path="/admin/:id/characters" element={<RequireGameCreator><AdminCharactersPage /></RequireGameCreator>} />
        <Route path="/admin/:id/consistency" element={<RequireGameCreator><AdminConsistencyPage /></RequireGameCreator>} />
        <Route path="/admin/:id/llm-logs" element={<RequireGameCreator><AdminLLMLogsPage /></RequireGameCreator>} />
        <Route path="/admin/:id/agent-calls" element={<RequireGameCreator><AdminAgentCallsPage /></RequireGameCreator>} />
        <Route path="/admin/:id/settings" element={<RequireGameCreator><GameSettingsPage /></RequireGameCreator>} />
        <Route path="/character/:id" element={<CharacterSheetPage />} />
        <Route path="/character/create" element={<CharacterCreateWizard />} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
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
