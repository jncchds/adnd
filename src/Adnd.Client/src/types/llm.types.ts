export interface LLMPreset {
  id: string
  userId: string
  name: string
  providerType: 'ollama' | 'openaicompatible' | 'openai' | 'google'
  baseModel: string
  endpointUrl: string | null
  temperature: number
  maxTokens: number
  topP: number
  frequencyPenalty: number
  presencePenalty: number
  stream: boolean
  timeoutMs: number | null
  reasoningEffort: 'none' | 'low' | 'medium' | 'high'
  embeddingModel: string | null
  embeddingEndpointUrl: string | null
  isActive: boolean
  isDefault: boolean
  createdAt: string
  updatedAt: string
}

export interface LLMPresetCreate {
  name: string
  providerType: string
  baseModel: string
  endpointUrl?: string
  apiKey?: string
  temperature?: number
  maxTokens?: number
  topP?: number
  frequencyPenalty?: number
  presencePenalty?: number
  stream?: boolean
  timeoutMs?: number
  reasoningEffort?: string
  embeddingModel?: string
  embeddingEndpointUrl?: string
  isDefault?: boolean
}

export interface LLMPresetUpdate extends LLMPresetCreate {
  id: string
}

export interface ProviderStatus {
  providerId: string
  isAvailable: boolean
  message: string
}

export type LLMInteractionStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed'

export interface LLMInteractionLog {
  id: string
  userId: string
  originGameId: string | null
  systemPrompt: string
  userPrompt: string
  response: string
  reasoning: string | null
  promptTokens: number
  completionTokens: number
  totalTokens: number
  durationMs: number
  presetName: string
  endpointUrl: string
  model: string
  startedAt: string
  status: LLMInteractionStatus
  errorMessage: string | null
  completedAt: string | null
}
