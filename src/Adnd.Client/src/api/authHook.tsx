import { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import { api, AuthUser } from './client';

interface AuthContextType {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    refreshUser();
  }, []);

  const refreshUser = useCallback(async () => {
    try {
      const userData = await api.getMe();
      setUser(userData);
    } catch {
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const login = async (email: string, password: string) => {
    const response = await api.login(email, password);
    api.setTokens(response.accessToken, response.refreshToken);
    setUser(response.user);
  };

  const register = async (email: string, password: string, displayName: string) => {
    const response = await api.register(email, password, displayName);
    api.setTokens(response.accessToken, response.refreshToken);
    setUser(response.user);
  };

  const logout = async () => {
    await api.logout();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{
      user,
      isAuthenticated: !!user,
      isLoading,
      login,
      register,
      logout,
      refreshUser,
    }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
