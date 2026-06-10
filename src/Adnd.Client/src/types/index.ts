export interface AuthResponse {
  id: string;
  email: string;
  displayName: string;
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

export interface User {
  id: string;
  email: string;
  displayName: string;
}

export interface LlmPreset {
  id: string;
  name: string;
  provider: string;
  baseUrlModel: string;
  embeddingModel: string;
  systemPrompt: string;
  temperature: number;
  maxTokens?: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ModelList {
  chatModels: string[];
  embeddingModels: string[];
}

export interface GameSystem {
  id: string;
  name: string;
  slug: string;
  description: string;
  type: 'predefined' | 'custom';
  rulesetConfig?: string;
  createdAt: string;
  updatedAt: string;
}

export interface Game {
  id: string;
  title: string;
  systemId: string;
  systemName: string;
  systemSlug: string;
  creatorId: string;
  creatorDisplayName: string;
  status: string;
  plotSeed?: string;
  joinCode: string;
  playerCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface GameDetail extends Game {
  players: GamePlayer[];
}

export interface GamePlayer {
  id: string;
  userId: string;
  role: string;
  characterName: string;
  spectating: boolean;
  isBanned: boolean;
}
