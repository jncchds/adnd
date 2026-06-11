export enum LLMProviderType {
  Ollama = 0,
  LmStudio = 1,
  OpenAI = 2,
  Google = 3,
}

export interface LLMPreset {
  id: string;
  name: string;
  providerType: string;
  baseModel: string;
  endpointUrl?: string;
  hasApiKey: boolean;
  temperature: number;
  maxTokens: number;
  topP: number;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface LLMInteractionLog {
  id: string;
  presetId?: string;
  presetName?: string;
  providerType: string;
  model: string;
  promptTokens?: number;
  completionTokens?: number;
  totalTokens?: number;
  durationMs: number;
  success: boolean;
  error?: string;
  systemPrompt?: string;
  userPrompt?: string;
  response?: string;
  requestJson?: string;
  responseJson?: string;
  origin: string;
  originGameId?: string;
  originSessionId?: string;
  originAgent?: string;
  originAction?: string;
  startedAt: string;
  completedAt: string;
}

export interface PresetUsageSummary {
  presetId: string;
  presetName: string;
  providerType: string;
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  totalTokens: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  avgDurationMs: number;
  lastUsed: string;
}

export interface GameProviderUsageSummary {
  providerType: string;
  model: string;
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  successRate: number;
  totalTokens: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  avgDurationMs: number;
  maxDurationMs: number;
  firstCall: string;
  lastCall: string;
}

export interface ProviderStatus {
  providerType: string;
  model: string;
  connected: boolean;
  lastChecked: string;
}

export interface ConsistencyReport {
  gameId: string;
  sessionId?: string;
  inconsistencies: string[];
  checkedAt: string;
}
