export type CombatStatus = 'Active' | 'Paused' | 'Finished'
export type ParticipantType = 'Character' | 'NPC' | 'Player'

export interface DeathSaveState {
  successes: number
  failures: number
  isDead: boolean
  isStable: boolean
}

export interface CombatParticipant {
  id: string
  combatId: string
  participantType: ParticipantType
  characterId: string | null
  npcId: string | null
  playerId: string | null
  displayName: string
  initiative: number
  initiativeCount: number
  hp: number
  maxHP: number
  ac: number
  conditions: string[]
  temporaryHP: Record<string, unknown> | null
  savingThrows: Record<string, unknown> | null
  deathSaveState: DeathSaveState | null
  actionsRemaining: number
  bonusActionsRemaining: number
  reactionsRemaining: number
  movementsRemaining: number
  freeActions: number
}

export interface Combat {
  id: string
  gameId: string
  sessionId: string
  name: string
  status: CombatStatus
  currentRound: number
  currentTurnIndex: number
  initiativeCount: number
  participants: CombatParticipant[]
  createdAt: string
  updatedAt: string
}
