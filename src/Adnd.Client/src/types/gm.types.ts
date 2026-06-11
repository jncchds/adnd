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
  gameId: string;
  sessionId?: string;
  toolId: string;
  toolName: string;
  category: ToolCategory;
  status: ToolCallStatus;
  prompt?: string;
  response?: string;
  error?: string;
  metadata?: Record<string, any>;
  createdAt: string;
  completedAt?: string;
}

export interface ToolCallConfirmationResponse {
  callId: string;
  toolId: string;
  toolName: string;
  prompt: string;
  metadata?: Record<string, any>;
  expiresAt: string;
}

export interface PlayerRollConfirmationResponse {
  callId: string;
  toolId: string;
  toolName: string;
  diceFormula: string;
  modifiers: Record<string, number>;
  targetDC?: number;
  expiresAt: string;
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
  id: string;
  name: string;
  description: string;
  category: ToolCategory;
  parameters: Record<string, any>;
  isActive: boolean;
}

export interface GMToolResponse {
  tool: GMToolDefinition;
  result?: Record<string, any>;
  error?: string;
}

export interface ExecuteToolRequest {
  toolId: string;
  parameters: Record<string, any>;
}

export interface ExecuteToolResponse {
  toolId: string;
  result: Record<string, any>;
  error?: string;
}

export interface DiceHistoryEntry {
  id: string;
  gameId: string;
  sessionId?: string;
  playerId?: string;
  playerName?: string;
  formula: string;
  result: number;
  breakdown: string[];
  metadata: Record<string, any>;
  createdAt: string;
}

export interface DiceHistoryResponse {
  gameId: string;
  entries: DiceHistoryEntry[];
  total: number;
  hasMore: boolean;
}

export interface CombatSummaryEntry {
  combatId: string;
  name?: string;
  status: string;
  currentRound: number;
  participantCount: number;
  startedAt: string;
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
  spells: SpellEntry[];
  spellSlots: SpellSlotInfo[];
}

export interface SpellUpdateRequest {
  spellName: string;
  castCount?: number;
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


