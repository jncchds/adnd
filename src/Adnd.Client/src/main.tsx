import React, { createContext, useContext, useState, useEffect } from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import { ThemeProvider, createTheme } from '@mui/material/styles'
import CssBaseline from '@mui/material/CssBaseline'
import './index.css'

// ── Color mode context ─────────────────────────────────────────
interface ColorModeCtx { mode: 'dark' | 'light'; toggle: () => void }
export const ColorModeContext = createContext<ColorModeCtx>({ mode: 'dark', toggle: () => {} })
export const useColorMode = () => useContext(ColorModeContext)

// ── DnD Arcane Dark / Light themes ────────────────────────────
const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#7c3aed' },
    secondary: { main: '#9f67fa' },
    background: { default: '#0c0a0e', paper: '#141019' },
    divider: '#2a2235',
    text: { primary: '#d4d0e8', secondary: '#7b7490' },
    error: { main: '#ef4444' },
    warning: { main: '#f59e0b' },
    success: { main: '#22c55e' },
  },
  typography: { fontFamily: "'Inter', system-ui, sans-serif" },
  components: {
    MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } },
  },
})

const lightTheme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '#6d28d9' },
    secondary: { main: '#7c3aed' },
    background: { default: '#f5f3f8', paper: '#ffffff' },
    divider: '#e2dced',
    text: { primary: '#1a1625', secondary: '#6b6480' },
    error: { main: '#ef4444' },
    warning: { main: '#d97706' },
    success: { main: '#16a34a' },
  },
  typography: { fontFamily: "'Inter', system-ui, sans-serif" },
})

// ── Root ──────────────────────────────────────────────────────
function Root() {
  const stored = (localStorage.getItem('adnd-theme') ?? 'dark') as 'dark' | 'light'
  const [mode, setMode] = useState<'dark' | 'light'>(stored)

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', mode)
  }, [mode])

  const toggle = () => {
    setMode(prev => {
      const next = prev === 'dark' ? 'light' : 'dark'
      localStorage.setItem('adnd-theme', next)
      return next
    })
  }

  return (
    <ColorModeContext.Provider value={{ mode, toggle }}>
      <ThemeProvider theme={mode === 'dark' ? darkTheme : lightTheme}>
        <CssBaseline />
        <App />
      </ThemeProvider>
    </ColorModeContext.Provider>
  )
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <Root />
  </React.StrictMode>,
)
