export enum MessageType {
  Chat = 0,
  Action = 1,
  Dice = 2,
  System = 3,
  GM = 4,
  PlayerWhisper = 5,
  GMWhisper = 6,
  AgentCall = 7,
  AgentResponse = 8,
}

export enum GameStatus {
  Draft = 0,
  Active = 1,
  Archived = 2,
  Finished = 3,
}

export enum PlayerRole {
  GM = 0,
  Player = 1,
  Spectator = 2,
  Observer = 3,
}

export enum PlayerStatus {
  Active = 0,
  Disconnected = 1,
  Left = 2,
}

export enum PlotThreadStatus {
  Active = 0,
  Resolved = 1,
  Abandoned = 2,
}

// Whisper types
export enum WhisperType {
  PlayerToPlayer = 0,
  PlayerToGM = 1,
  GMToPlayer = 2,
  GMToGroup = 3,
  GMToAll = 4,
}

// Agent framework types
export enum AgentType {
  GM = 0,
  LLM = 1,
  Dice = 2,
  RAG = 3,
  NPC = 4,
  Player = 5,
  System = 6,
}

export enum AgentAction {
  Query = 0,
  Generate = 1,
  Roll = 2,
  Check = 3,
  Narrate = 4,
  Suggest = 5,
  Execute = 6,
  Notify = 7,
  Recall = 8,
  ManageState = 9,
}

export enum AgentCallStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4,
}

// LLM Preset types
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
  baseUrlModel: string;
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
