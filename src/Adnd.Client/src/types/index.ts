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
