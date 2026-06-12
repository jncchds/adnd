import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'

import { AuthProvider } from './api/hooks/useAuth'
import AppShell from './components/AppShell'
import HomePage from './pages/HomePage'
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
import AuthPage from './pages/AuthPage'
import LLMPresetsPage from './pages/LLMPresetsPage'
import SystemsPage from './pages/SystemsPage'
import UserSettingsPage from './pages/UserSettingsPage'

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          {/* Auth routes (no layout) */}
          <Route path="/login" element={<AuthPage />} />
          <Route path="/register" element={<AuthPage />} />

          {/* Main layout */}
          <Route element={<AppShell />}>
            <Route path="/" element={<HomePage />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/llm-presets" element={<LLMPresetsPage />} />
            <Route path="/llm-presets/new" element={<LLMPresetsPage />} />
            <Route path="/systems" element={<SystemsPage />} />
            <Route path="/systems/new" element={<SystemsPage />} />
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
          </Route>

          {/* Fallback */}
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
