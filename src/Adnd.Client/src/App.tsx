import { BrowserRouter, Routes, Route, Navigate, Link } from 'react-router-dom'
import { Container, Typography, AppBar, Toolbar, Button } from '@mui/material'
import { AuthProvider } from './api/authHook'
import HomePage from './pages/HomePage'
import DashboardPage from './pages/DashboardPage'
import GameRoomPage from './pages/GameRoomPage'
import AdminPage from './pages/AdminPage'
import CharacterSheetPage from './pages/CharacterSheetPage'
import AuthPage from './pages/AuthPage'
import { api } from './api/client'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  if (!api.isAuthenticated()) {
    return <Navigate to="/login" />;
  }
  return <>{children}</>;
}

function App() {
  const authenticated = api.isAuthenticated();
  return (
    <AuthProvider>
      <BrowserRouter>
        <AppBar position="static" sx={{ bgcolor: '#1a1a1a' }}>
          <Toolbar>
            <Typography variant="h6" component={Link} to="/" sx={{ flexGrow: 1, textDecoration: 'none', color: 'inherit', cursor: 'pointer' }}>
              ADnD
            </Typography>
            <Button color="inherit" component={Link} to="/dashboard">Dashboard</Button>
            {!authenticated && (
              <>
                <Button color="inherit" component={Link} to="/login">Login</Button>
                <Button color="inherit" component={Link} to="/register">Register</Button>
              </>
            )}
          </Toolbar>
        </AppBar>
        <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/dashboard" element={
              <ProtectedRoute>
                <DashboardPage />
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
            <Route path="/login" element={<AuthPage />} />
            <Route path="/register" element={<AuthPage />} />
            <Route path="*" element={<Navigate to="/dashboard" />} />
          </Routes>
        </Container>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
