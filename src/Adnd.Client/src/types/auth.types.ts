/** Matches UserDto(Id, Email, DisplayName) — the API sends nothing else. */
export interface User {
  id: string
  email: string
  displayName: string
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
