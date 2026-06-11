import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'

import { AuthProvider } from './api/hooks/useAuth'
import AppShell from './components/AppShell'
import HomePage from './pages/HomePage'
import DashboardPage from './pages/DashboardPage'
import GameRoomPage from './pages/GameRoomPage'
import AdminPage from './pages/AdminPage'
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
            <Route path="/game/:id" element={<GameRoomPage />} />
            <Route path="/game/:id/combat" element={<GameRoomPage />} />
            <Route path="/game/:id/settings" element={<GameRoomPage />} />
            <Route path="/admin/:id" element={<AdminPage />} />
            <Route path="/admin/:id/plot-board" element={<AdminPage />} />
            <Route path="/admin/:id/npcs" element={<AdminPage />} />
            <Route path="/admin/:id/characters" element={<AdminPage />} />
            <Route path="/admin/:id/consistency" element={<AdminPage />} />
            <Route path="/admin/:id/llm-logs" element={<AdminPage />} />
            <Route path="/admin/:id/agent-calls" element={<AdminPage />} />
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
