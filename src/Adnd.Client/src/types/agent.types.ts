export enum AgentType {
  Creator = 0,
  GM = 1,
  LLM = 2,
  Dice = 3,
  RAG = 4,
  NPC = 5,
  Player = 6,
  System = 7,
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
  Nudge = 10,
  CreateCharacter = 11,
}

export enum AgentCallStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4,
}

export interface AgentCallItem {
  id: string;
  gameId: string;
  sessionId?: string;
  agentType: AgentType;
  action: AgentAction;
  status: AgentCallStatus;
  prompt?: string;
  response?: string;
  error?: string;
  metadata?: Record<string, any>;
  createdAt: string;
  completedAt?: string;
}

export interface PendingAgentCall {
  id: string;
  fromAgent: number;
  toAgent: number;
  action: number;
  status: string; // 'pending' | 'running'
  input?: string;
  createdAt: string;
  startedAt?: string;
  outputMessage?: string;
}
