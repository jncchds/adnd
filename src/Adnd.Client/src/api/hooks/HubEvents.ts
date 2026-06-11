// ==================== Event Interfaces ====================

export interface WhisperResponse {
  whisperId: string;
  gameId: string;
  sessionId: string;
  playerId: string;
  playerName: string;
  type: number;
  content: string;
  isOOC: boolean;
  createdAt: string;
}

export interface AgentCallStartedEvent {
  callId: string;
  gameId: string;
  agentType: number;
  action: number;
  prompt: string;
  metadata: Record<string, any>;
}

export interface AgentCallCompletedEvent {
  callId: string;
  gameId: string;
  response: string;
  error?: string;
}

export interface ToolCallNotificationEvent {
  callId: string;
  toolName: string;
  outputMessage: string;
  requiresConfirmation: boolean;
  timestamp: string;
}

export interface ToolCallConfirmedEvent {
  callId: string;
  toolName: string;
  approved: boolean;
  outputMessage?: string;
  status: number;
}

export interface PlayerRollRequestedEvent {
  callId: string;
  toolName: string;
  skill: string;
  formula: string;
  dc: number;
  context: string;
  optional: boolean;
  expiresAt: string;
}

export interface PlayerRollConfirmedEvent {
  callId: string;
  toolName: string;
  approved: boolean;
  skill: string;
  formula: string;
  dc: number;
  context: string;
  optional: boolean;
}

export interface PlayerRollDeclinedEvent {
  callId: string;
  toolName: string;
}

export interface CombatStartedEvent {
  combatId: string;
  name?: string;
  participants: CombatParticipantEvent[];
}

export interface CombatParticipantEvent {
  id: string;
  displayName: string;
  participantType: string;
  currentHP: number;
  maxHP: number;
  ac: number;
  initiative: number;
  conditions: ConditionEntry[];
}

export interface CombatEndedEvent {
  combatId: string;
  name?: string;
  winner?: string;
}

export interface CombatParticipantAddedEvent {
  combatId: string;
  participant: CombatParticipantEvent;
}

export interface CombatParticipantRemovedEvent {
  combatId: string;
  participantId: string;
}

export interface InitiativeRolledEvent {
  combatId: string;
  participantId: string;
  displayName: string;
  initiative: number;
}

export interface InitiativeCompleteEvent {
  combatId: string;
  initiativeOrder: InitiativeRollEvent[];
}

export interface InitiativeRollEvent {
  participantId: string;
  displayName: string;
  initiative: number;
  rolls: number[];
}

export interface TurnAdvancedEvent {
  combatId: string;
  turnIndex: number;
  participantId: string;
  displayName: string;
}

export interface CombatAttackEvent {
  combatId: string;
  attacker: string;
  weapon: string;
  target: string;
  hit: boolean;
  isCritical: boolean;
  isFumble: boolean;
  attackRoll: number;
  attackDice: number;
  ac: number;
  damageDice: number;
  damageTotal: number;
  damageInfo: string;
  targetHP: number;
  targetMaxHP: number;
}

export interface CombatDamageEvent {
  combatId: string;
  target: string;
  damage: number;
  damageInfo: string;
  targetHP: number;
  targetMaxHP: number;
}

export interface CombatHealEvent {
  combatId: string;
  target: string;
  healAmount: number;
  targetHP: number;
  targetMaxHP: number;
}

export interface CombatSaveThrowEvent {
  combatId: string;
  participant: string;
  saveType: string;
  diceRoll: number;
  dc: number;
  success: boolean;
}

export interface CombatDeathSaveEvent {
  combatId: string;
  participant: string;
  success: boolean;
  successes: number;
  failures: number;
  isStabilized: boolean;
  isDead: boolean;
}

export interface ConditionAppliedEvent {
  combatId: string;
  participantId: string;
  condition: string;
  duration: number;
  description?: string;
}

export interface ConditionRemovedEvent {
  combatId: string;
  participantId: string;
  condition: string;
}

export interface CombatLogUpdatedEvent {
  combatId: string;
  events: CombatLogEvent[];
}

export interface CombatLogEvent {
  id: string;
  round: number;
  turnIndex: number;
  type: string;
  actorName: string;
  targetName: string;
  content: string;
  createdAt: string;
}

export interface ConditionEntry {
  name: string;
  duration: number;
  description?: string;
}

export interface PlayerDisconnectedEvent {
  gameId: string;
  playerId: string;
  playerName: string;
  disconnectedAt: string;
}

export interface PlayerReconnectedEvent {
  gameId: string;
  playerId: string;
  playerName: string;
  reconnectedAt: string;
}

export interface CombatLog {
  combatId: string;
  name?: string;
  status: string;
  currentRound: number;
  currentTurnIndex: number;
  participants: CombatParticipantSummary[];
  events: CombatLogEvent[];
}

export interface CombatParticipantSummary {
  id: string;
  displayName: string;
  participantType: string;
  currentHP: number;
  maxHP: number;
  ac: number;
  initiative: number;
  conditions: ConditionEntry[];
  isCurrentTurn: boolean;
  isDead: boolean;
}

