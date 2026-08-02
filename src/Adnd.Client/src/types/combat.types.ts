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
}

/** CombatDto */
export interface Combat {
  id: string
  gameId: string
  sessionId: string
  name: string
  status: CombatStatus
  currentRound: number
  currentTurnIndex: number
  participants: CombatParticipant[]
}

/** DamageDto */
export interface DamageEvent {
  combatId: string
  targetId: string
  amount: number
  damageType: string
  newHP: number
}

/** Anonymous payload broadcast by GameHub.HealParticipant */
export interface HealEvent {
  combatId: string
  targetId: string
  amount: number
  newHP: number
}

/** ConditionDto */
export interface ConditionEvent {
  combatId: string
  participantId: string
  condition: string
  applied: boolean
}
