import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'

import { AuthProvider } from './api/authHook'
import HomePage from './pages/HomePage'
import DashboardPage from './pages/DashboardPage'
import GameRoomPage from './pages/GameRoomPage'
import AdminPage from './pages/AdminPage'
import CharacterSheetPage from './pages/CharacterSheetPage'
import AuthPage from './pages/AuthPage'
import LLMPresetsPage from './pages/LLMPresetsPage'
import Layout from './components/Layout'
import { api } from './api/client'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  if (!api.isAuthenticated()) {
    return <Navigate to="/login" />;
  }
  return <>{children}</>;
}

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          {/* Auth routes (no layout) */}
          <Route path="/login" element={<AuthPage />} />
          <Route path="/register" element={<AuthPage />} />

          {/* Layout routes */}
          <Route element={<Layout />}>
            <Route path="/" element={<HomePage />} />
            <Route path="/dashboard" element={
              <ProtectedRoute>
                <DashboardPage />
              </ProtectedRoute>
            } />
            <Route path="/llm-presets" element={
              <ProtectedRoute>
                <LLMPresetsPage />
              </ProtectedRoute>
            } />
            <Route path="/game/:id" element={
              <ProtectedRoute>
                <GameRoomPage />
              </ProtectedRoute>
            } />
            <Route path="/admin/:id" element={
              <ProtectedRoute>
                <AdminPage />
              </ProtectedRoute>
            } />
            <Route path="/character/:id" element={
              <ProtectedRoute>
                <CharacterSheetPage />
              </ProtectedRoute>
            } />
          </Route>

          {/* Fallback */}
          <Route path="*" element={<Navigate to="/dashboard" />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
