import { StrictMode, createContext, useContext, useState, useMemo } from 'react'
import { createRoot } from 'react-dom/client'
import { ThemeProvider, createTheme, CssBaseline } from '@mui/material'
import App from './App'
import './index.css'

type ColorMode = 'light' | 'dark'
interface ColorModeCtx { mode: ColorMode; toggleColorMode: () => void }

export const ColorModeContext = createContext<ColorModeCtx>({
  mode: 'dark',
  toggleColorMode: () => {},
})

export function useColorMode() { return useContext(ColorModeContext) }

function Root() {
  const stored = localStorage.getItem('adnd-theme') as ColorMode | null
  const [mode, setMode] = useState<ColorMode>(stored ?? 'dark')

  const colorMode = useMemo(() => ({
    mode,
    toggleColorMode: () => {
      setMode(prev => {
        const next = prev === 'dark' ? 'light' : 'dark'
        localStorage.setItem('adnd-theme', next)
        return next
      })
    },
  }), [mode])

  const theme = useMemo(() => createTheme({
    palette: {
      mode,
      primary: { main: '#7c3aed' },
      secondary: { main: '#a78bfa' },
      background: mode === 'dark'
        ? { default: '#0c0a0e', paper: '#141019' }
        : { default: '#f4f1ff', paper: '#ffffff' },
      text: mode === 'dark'
        ? { primary: '#e9e3f5', secondary: '#a78bfa' }
        : { primary: '#1a0a2e', secondary: '#5b21b6' },
    },
    shape: { borderRadius: 8 },
    typography: {
      fontFamily: '"Inter", "Segoe UI", sans-serif',
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          ':root': {
            '--adnd-accent': '#7c3aed',
            '--adnd-bg': mode === 'dark' ? '#0c0a0e' : '#f4f1ff',
            '--adnd-surface': mode === 'dark' ? '#141019' : '#ffffff',
          },
        },
      },
    },
  }), [mode])

  return (
    <ColorModeContext.Provider value={colorMode}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <App />
      </ThemeProvider>
    </ColorModeContext.Provider>
  )
}

const root = document.getElementById('root')
if (!root) throw new Error('Root element not found')

createRoot(root).render(
  <StrictMode>
    <Root />
  </StrictMode>
)
