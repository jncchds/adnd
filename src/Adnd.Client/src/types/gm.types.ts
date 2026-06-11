export enum ToolCallStatus {
  Pending = 0,
  WaitingConfirmation = 1,
  Confirmed = 2,
  Denied = 3,
  Failed = 4,
  Completed = 5,
}

export enum ToolCategory {
  General = 0,
  Combat = 1,
  Narrative = 2,
  System = 3,
  Roll = 4,
}

export interface ToolCallInfo {
  id: string;
  toolName: string;
  status: ToolCallStatus;
  arguments?: string;
  outputMessage?: string;
  createdAt: string;
  requiresConfirmation: boolean;
}

export interface PendingToolCall extends ToolCallInfo {
  argumentsParsed?: Record<string, unknown>;
  outputMessage?: string;
}

export interface ToolCallConfirmationResponse {
  id: string;
  toolName: string;
  approved: boolean;
  outputMessage?: string;
  status: ToolCallStatus;
}

export interface PlayerRollConfirmationResponse {
  toolCallId: string;
  approved: boolean;
  skill: string;
  formula: string;
  dc: number;
  context: string;
  optional: boolean;
}

export interface ToolCallNotification {
  callId: string;
  toolId: string;
  toolName: string;
  category: ToolCategory;
  prompt?: string;
  response?: string;
  error?: string;
  status: ToolCallStatus;
  expiresAt?: string;
  timestamp: string;
}

export interface GMToolDefinition {
  name: string;
  description: string;
  category: ToolCategory;
  requiresConfirmation: boolean;
  parameters: Record<string, any>;
}

export interface GMToolResponse {
  gameId: string;
  count: number;
  tools: GMToolDefinition[];
}

export interface ExecuteToolRequest {
  toolName: string;
  arguments: string;
}

export interface ExecuteToolResponse {
  toolName: string;
  success: boolean;
  output: string | null;
  outputMessage: string | null;
  requiresUserInput: boolean;
  userInputType: string | null;
}

export interface DiceHistoryEntry {
  id: string;
  formula: string;
  total: number | null;
  diceCount: number | null;
  diceType: number | null;
  modifier: number;
  rolls: number[];
  sessionId: string | null;
  sessionTitle: string | null;
  playerId: string | null;
  characterName: string | null;
  createdAt: string;
}

export interface DiceHistoryResponse {
  gameId: string;
  count: number;
  rolls: DiceHistoryEntry[];
}

export interface CombatSummaryEntry {
  id: string;
  name: string;
  status: string;
  currentRound: number;
  participantCount: number;
  eventCount: number;
  startedAt: string;
  endedAt: string | null;
  sessionId: string | null;
  sessionTitle: string | null;
}

export interface CombatLogResponse {
  combatId: string;
  name?: string;
  status: string;
  currentRound: number;
  currentTurnIndex: number;
  participants: CombatParticipantEntry[];
  events: CombatEventEntry[];
}

import type { ConditionEntry } from './combat.types';

export interface CombatParticipantEntry {
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

export interface CombatEventEntry {
  id: string;
  round: number;
  turnIndex: number;
  type: string;
  actorName: string;
  targetName: string;
  content: string;
  createdAt: string;
}

export interface SpellEntry {
  name: string;
  level: number;
  castCount: number;
  maxCount: number;
  isPrepared?: boolean;
  isKnown?: boolean;
  damageBonus?: number;
  damageType?: string;
}

export interface SpellSlotInfo {
  level: number;
  total: number;
  used: number;
}

export interface SpellManagementResponse {
  characterId: string;
  characterName: string;
  characterClass: string;
  characterLevel: number;
  spells: SpellEntry[];
  spellSlots: SpellSlotInfo[];
  updatedAt: string;
}

export interface SpellUpdateRequest {
  name?: string;
  level?: string;
  school?: string;
  castingTime?: string;
  range?: string;
  duration?: string;
  components?: string;
  description?: string;
  saveType?: string;
  saveDC?: number;
  damageFormula?: string;
  damageBonus?: number;
  damageType?: string;
  isPrepared?: boolean;
  isKnown?: boolean;
  spellSlots?: SpellSlotInfo[];
}

export interface SessionNote {
  id: string;
  sessionId: string;
  creatorId: string;
  creatorName: string;
  title: string;
  content: string;
  createdAt: string;
  updatedAt?: string;
}

export interface DiceStatsResponse {
  gameId: string;
  sessionId?: string;
  totalRolls: number;
  averageRoll: number;
  minRoll: number;
  maxRoll: number;
  medianRoll: number;
  diceTypes: Record<number, number>;
  distribution: Record<number, number>;
}

export interface PlayerDiceStatsResponse {
  gameId: string;
  sessionId?: string;
  playerId: string;
  playerName: string;
  totalRolls: number;
  averageRoll: number;
  minRoll: number;
  maxRoll: number;
  medianRoll: number;
  naturalTwenties: number;
  naturalOnes: number;
  diceTypes: Record<number, number>;
  distribution: Record<number, number>;
}

export interface MessagePaginationResponse {
  gameId: string;
  sessionId: string;
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  hasMore: boolean;
  messages: MessagePaginated[];
}

export interface MessagePaginated {
  id: string;
  sessionId: string;
  playerId?: string;
  playerName: string;
  content: string;
  type: number;
  isOOC: boolean;
  metadata: Record<string, any>;
  createdAt: string;
}

export interface MessageSearchResponse {
  gameId: string;
  sessionId: string;
  query: string;
  results: MessageSearchResult[];
}

export interface MessageSearchResult {
  id: string;
  content: string;
  type: number;
  playerName: string;
  isOOC: boolean;
  createdAt: string;
  distance: number;
}


