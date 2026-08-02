export type AgentCallStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'
export type AgentType = 'Creator' | 'GM' | 'LLM' | 'Dice' | 'RAG' | 'NPC' | 'Player' | 'System'
export type AgentAction =
  | 'Query' | 'Generate' | 'Roll' | 'Check' | 'Narrate' | 'Suggest'
  | 'Execute' | 'Notify' | 'Recall' | 'ManageState' | 'Nudge'
  | 'CreateCharacter' | 'OpenNarrative' | 'GenerateInitialThreads'

export type SagaStep =
  | 'None' | 'Init' | 'LLMDispatch' | 'LLMResponse' | 'ToolExecution'
  | 'ToolCoordination' | 'LLMFollowUp' | 'NarrativeReady' | 'Completed' | 'Failed'

export interface AgentStepEvent {
  step: SagaStep
  at: string
}

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
  stepHistory: AgentStepEvent[]
  durationMs: number | null
  createdAt: string
  updatedAt: string
}

export interface ToolCall {
  id: string
  gameId: string
  sessionId: string
  toolName: string
  arguments: Record<string, unknown>
  targetPlayerId: string | null
  startedAt: string
}

export type GMToolCallStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'AwaitingConfirmation' | 'Declined'

export interface GMToolCallSummary {
  id: string
  toolName: string
  status: GMToolCallStatus
  arguments: Record<string, unknown>
  result: unknown
  startedAt: string
  completedAt: string | null
  requiresConfirmation: boolean
  targetPlayerId: string | null
}
