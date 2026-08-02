export type GameStatus = 'Draft' | 'Starting' | 'Active' | 'Archived'
export type GMStatus = 'Idle' | 'Running' | 'Paused'
export type PlayerRole = 'Creator' | 'Player' | 'Spectator' | 'Observer'
export type PlayerStatus = 'Active' | 'Inactive' | 'Banned'

export interface Game {
  id: string
  creatorId: string
  name: string
  systemId: string
  systemVersion: string | null
  language: string
  plotSeed: string | null
  status: GameStatus
  gmStatus: GMStatus
  lastGMAction: string | null
  lastGMActionAt: string | null
  inviteCode: string | null
  llmPresetId: string | null
  currentSessionId: string | null
  createdAt: string
  updatedAt: string
  isDeleted?: boolean
  deletedAt?: string | null
}

export interface Player {
  id: string
  gameId: string
  userId: string
  characterName: string | null
  role: PlayerRole
  status: PlayerStatus
  isConnected: boolean
  /** Projected from the linked User by PlayerDto — always present. */
  displayName: string
}

export interface GameSession {
  id: string
  gameId: string
  title: string | null
  description: string | null
  status: string
  createdAt: string
  closedAt: string | null
}

export interface NPC {
  id: string
  gameId: string
  name: string
  description: string | null
  attributes: Record<string, number> | null
  skills: Record<string, number> | null
  inventory: unknown[] | null
  attitude?: 'Friendly' | 'Neutral' | 'Unfriendly' | 'Hostile'
  faction?: string | null
  // Dead/Departed NPCs stay on record but drop out of the cast the GM is shown each turn.
  status?: 'Active' | 'Dead' | 'Departed'
  lastSeenAt?: string | null
  isDeleted: boolean
}

export interface Character {
  id: string
  playerId: string
  name: string
  class: string
  level: number
  proficiencyBonus: number
  currentHP: number
  maxHP: number
  attributes: Record<string, number>
  skills: Record<string, unknown>
  inventory: unknown[]
  spells: unknown
  conditions: unknown[]
  customFields: Record<string, unknown>
  spellSlots: unknown
  background: string | null
  backgroundSkills: string | null
  backgroundProficiencies: string | null
  backgroundFeatures: string | null
  backstory: string | null
  spellcastingAbility: string | null
  spellSaveDC: number
  spellAttackBonus: number
  isDeleted: boolean
}

export interface CreateCharacterRequest {
  gameId: string
  name: string
  class: string
  background: string
  backstory?: string
  attributes?: Record<string, number>
}

export interface GameCreateRequest {
  name: string
  systemId: string
  language?: string
  plotSeed?: string
  llmPresetId?: string
}

export interface JoinByCodeRequest {
  code: string
}
