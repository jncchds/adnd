import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { CircularProgress, Box } from '@mui/material'
import { AuthProvider, useAuth } from './context/AuthContext'
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
import LLMPresetsPage from './pages/LLMPresetsPage'
import SystemsPage from './pages/SystemsPage'
import UserSettingsPage from './pages/UserSettingsPage'

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth()
  if (loading) return <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh' }}><CircularProgress /></Box>
  if (!user) return <Navigate to="/login" replace />
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
        <Route path="/game/:id/settings" element={<GameSettingsPage />} />
        <Route path="/admin/:id" element={<AdminDashboardPage />} />
        <Route path="/admin/:id/plot-board" element={<AdminPlotBoardPage />} />
        <Route path="/admin/:id/npcs" element={<AdminNPCsPage />} />
        <Route path="/admin/:id/characters" element={<AdminCharactersPage />} />
        <Route path="/admin/:id/consistency" element={<AdminConsistencyPage />} />
        <Route path="/admin/:id/llm-logs" element={<AdminLLMLogsPage />} />
        <Route path="/admin/:id/agent-calls" element={<AdminAgentCallsPage />} />
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
