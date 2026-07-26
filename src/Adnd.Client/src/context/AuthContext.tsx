import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react'
import { api } from '../api/client'
import type { User } from '../types'

interface AuthCtx {
  user: User | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string) => Promise<void>
  logout: () => Promise<void>
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthCtx>({
  user: null, loading: true,
  login: async () => {}, register: async () => {}, logout: async () => {}, refresh: async () => {},
})

export function useAuth() { return useContext(AuthContext) }

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    const token = api.getToken()
    if (!token) { setLoading(false); return }
    try {
      const me = await api.auth.me()
      setUser(me)
    } catch {
      api.clearTokens()
      setUser(null)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const login = useCallback(async (email: string, password: string) => {
    const res = await api.auth.login({ email, password })
    api.setTokens(res.token, res.refreshToken)
    setUser(res.user)
  }, [])

  const register = useCallback(async (email: string, password: string, displayName: string) => {
    const res = await api.auth.register({ email, password, displayName })
    api.setTokens(res.token, res.refreshToken)
    setUser(res.user)
  }, [])

  const logout = useCallback(async () => {
    try { await api.auth.logout() } catch { /* ignore */ }
    api.clearTokens()
    setUser(null)
  }, [])

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout, refresh }}>
      {children}
    </AuthContext.Provider>
  )
}
