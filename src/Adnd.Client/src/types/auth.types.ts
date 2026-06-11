export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  role?: string; // Current game role (Creator, Player, Spectator, Observer)
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
}
