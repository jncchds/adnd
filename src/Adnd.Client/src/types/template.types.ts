export interface PromptTemplate {
  id: string
  gameId: string | null
  name: string
  type: string
  content: string
  isDefault: boolean
  createdAt: string
}

export interface GameTemplate {
  id: string
  userId: string
  llmPresetId: string | null
  name: string
  description: string | null
  systemId: string
  language: string
  plotSeed: string | null
  gameParameters: string | null
  createdAt: string
  updatedAt: string
}

export interface SessionNote {
  id: string
  gameId: string
  sessionId: string | null
  title: string
  content: string
  createdAt: string
  updatedAt: string
}
