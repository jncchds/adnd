// These mirror the DTOs broadcast by GameHub (see Hubs/ResponseDTOs.cs), not the EF
// entities. They previously described the entities, so most fields the UI read were
// never actually on the wire and silently rendered as undefined.

export type CombatStatus = 'Active' | 'Paused' | 'Finished'
export type ParticipantType = 'Character' | 'NPC' | 'Neutral'

export interface DeathSaveState {
  successes: number
  failures: number
  isDead: boolean
  isStable: boolean
}

/** ParticipantDto */
export interface CombatParticipant {
  id: string
  displayName: string
  initiative: number
  hp: number
  maxHP: number
  ac: number
  participantType: ParticipantType
  conditions: string[]
  actionsRemaining: number
  bonusActionsRemaining: number
  reactionsRemaining: number
  movementsRemaining: number
  deathSaveState: DeathSaveState | null
  /** CombatZone.id of the zone this combatant stands in. */
  zoneId: string | null
  /** Set for player characters — how the table knows whose turn is theirs. */
  characterId: string | null
  playerId: string | null
}

/** CombatZoneDto. Zones are in order; neighbours are adjacent. */
export interface CombatZone {
  id: string
  name: string
  tags: string[]
}

/** CombatDto */
export interface Combat {
  id: string
  gameId: string
  sessionId: string
  name: string
  status: CombatStatus
  currentRound: number
  /** Index into `participants`, which arrive in turn order. */
  currentTurnIndex: number
  zones: CombatZone[]
  participants: CombatParticipant[]
}
