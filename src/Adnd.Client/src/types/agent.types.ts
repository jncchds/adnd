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
  status: GMToolCallStatus
  /** For AwaitingReroll, the interim roll and the abilities offered on it. */
  result: PendingRerollResult | null
}

export type GMToolCallStatus =
  | 'Pending' | 'Running' | 'Completed' | 'Failed'
  | 'AwaitingConfirmation' | 'Declined' | 'AwaitingReroll'

export interface RerollOption {
  featureId: string
  name: string
  description: string
  usesRemaining: number | null
}

export interface PendingRerollResult {
  content: string
  total: number
  success: boolean | null
  options: RerollOption[]
}

/**
 * Pushed over SignalR ("RerollOffered") to the roller alone. toolCallId is null for a roll
 * the player made themselves — nothing is waiting on the answer, so it resolves over the hub
 * rather than through /api/gmtools.
 */
export interface RerollOffer {
  toolCallId: string | null
  gameId: string
  content: string
  options: RerollOption[]
}

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
