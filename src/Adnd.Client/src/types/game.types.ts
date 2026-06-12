import type { ConditionEntry } from './combat.types';
import type { SpellEntry, SpellSlotInfo } from './gm.types';
import type { PendingAgentCall } from './agent.types';

export enum MessageType {
  // === In-game messages (influence narrative) ===
  InGamePublic = 0,
  InGameWhisper = 1,

  // === OOC messages (never influence narrative) ===
  OOCPublic = 2,
  OOCWhisper = 3,

  // === System / meta messages ===
  Action = 4,
  Dice = 5,
  System = 6,
  GM = 7,
  AgentCall = 8,
  AgentResponse = 9,
}

export enum GameStatus {
  Draft = 0,
  Active = 1,
  Archived = 2,
  Finished = 3,
}

export enum GMStatus {
  Idle = 0,
  Running = 1,
  Paused = 2,
}

export enum PlayerRole {
  Creator = 0,
  Player = 1,
  Spectator = 2,
  Observer = 3,
}

export enum PlayerStatus {
  Active = 0,
  Left = 1,
}


export interface GameListItem {
  id: string;
  creatorId: string;
  creatorName: string;
  name: string;
  systemId: string;
  systemVersion?: string;
  status: string;
  gmStatus: string; // 'idle' | 'running' | 'paused'
  createdAt: string;
  inviteCode?: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language?: string;
}

export interface GameDetail {
  id: string;
  creatorId: string;
  creatorName: string;
  name: string;
  systemId: string;
  systemVersion?: string;
  status: string;
  gmStatus: string; // 'idle' | 'running' | 'paused'
  createdAt: string;
  inviteCode?: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language?: string;
}

export interface InviteResponse {
  inviteCode: string;
  inviteUrl: string;
}

export interface GMStatusResponse {
  gameId: string;
  status: string; // 'idle' | 'running' | 'paused'
  lastAction?: string;
  lastActionAt?: string;
}

export interface SwayResponse {
  gameId: string;
  callId: string;
  status: number;
  createdAt: string;
}

export interface GameSessionListItem {
  id: string;
  title: string;
  description?: string;
  startedAt: string;
  endedAt?: string;
  messageCount: number;
}

export interface GameSessionDetail {
  id: string;
  title: string;
  description?: string;
  startedAt: string;
  endedAt?: string;
}

export interface PlayerListItem {
  id: string;
  characterName: string;
  role: string;
  status: string;
  joinedAt: string;
  character?: CharacterDetail;
  userName?: string;
  userEmail?: string;
}

export interface NPCListItem {
  id: string;
  name: string;
  description?: string;
  attributes: JsonElement;
  skills: JsonElement;
  inventory: JsonElement;
  spells: JsonElement;
  plotThreadId?: string;
  createdAt: string;
}

export interface NPCUpdateRequest {
  name?: string;
  description?: string;
  attributes?: JsonElement;
  skills?: JsonElement;
  inventory?: JsonElement;
  spells?: JsonElement;
  plotThreadId?: string;
}

export interface PlotThreadListItem {
  id: string;
  title: string;
  description: string;
  status: string;
  keyEventMessageIds: string[];
  createdAt: string;
  updatedAt?: string;
}

export interface PlotThreadUpdateRequest {
  title?: string;
  description?: string;
  status?: string;
  keyEventMessageIds?: string[];
}

export interface CharacterListItem {
  id: string;
  name: string;
  characterClass: string;
  level: number;
  systemId: string;
  gameName: string;
  createdAt: string;
}

export interface CharacterDetail {
  id: string;
  name: string;
  characterClass: string;
  class: string;
  level: number;
  systemId: string;
  gameName: string;
  playerUserId?: string;
  playerName?: string;
  stats: Record<string, number>;
  conditions: ConditionEntry[];
  spells: SpellEntry[];
  spellSlots: SpellSlotInfo[];
  equipment: JsonElement;
  inventory: JsonElement;
  skills: JsonElement;
  attributes: JsonElement;
  notes: string;
  maxHP: number;
  currentHP: number;
  proficiencyBonus: number;
  createdAt: string;
  updatedAt: string;
}

export interface CharacterUpdateRequest {
  name?: string;
  characterClass?: string;
  level?: number;
  stats?: Record<string, number>;
  conditions?: ConditionEntry[];
  spells?: SpellEntry[];
  spellSlots?: SpellSlotInfo[];
  equipment?: JsonElement;
  notes?: string;
}

export interface PendingCallsResponse {
  pendingCalls: PendingAgentCall[];
  pendingCount: number;
  runningCount: number;
}

import type { JsonElement } from './llm.types';

// Re-export for convenience
export type { JsonElement };
