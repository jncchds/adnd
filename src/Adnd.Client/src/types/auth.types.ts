export interface User {
  id: string
  email: string
  displayName: string
  createdAt: string
  lastLoginAt: string | null
}

export interface AuthResponse {
  token: string
  refreshToken: string
  user: User
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  displayName: string
}
