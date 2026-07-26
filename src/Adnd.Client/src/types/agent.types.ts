export type AgentCallStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'
export type AgentType = 'Creator' | 'GM' | 'LLM' | 'Dice' | 'RAG' | 'NPC' | 'Player' | 'System'
export type AgentAction =
  | 'Query' | 'Generate' | 'Roll' | 'Check' | 'Narrate' | 'Suggest'
  | 'Execute' | 'Notify' | 'Recall' | 'ManageState' | 'Nudge'
  | 'CreateCharacter' | 'OpenNarrative' | 'GenerateInitialThreads'

export interface AgentCall {
  id: string
  gameId: string
  sessionId: string | null
  fromAgent: AgentType
  toAgent: AgentType
  action: AgentAction
  input: string | null
  output: string | null
  outputMessage: string | null
  status: AgentCallStatus
  error: string | null
  parentCallId: string | null
  currentStep: number
  durationMs: number | null
  createdAt: string
  updatedAt: string
}

export interface ToolCall {
  id: string
  name: string
  arguments: Record<string, unknown>
  status: 'Pending' | 'Confirmed' | 'Declined'
}
