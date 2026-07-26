export type MessageType =
  | 'Player' | 'GM' | 'System' | 'OOC' | 'Whisper' | 'OOCWhisper'
  | 'DiceRoll' | 'SkillCheck' | 'AttackRoll' | 'SpellCast'
  | 'CombatStart' | 'CombatEnd' | 'CombatAttack' | 'CombatDamage'
  | 'CombatHealing' | 'CombatCondition' | 'CombatInitiative' | 'CombatTurn'
  | 'CombatDeath' | 'CombatRevival' | 'CombatDeathSave'
  | 'PlayerJoined' | 'PlayerLeft' | 'PlayerDisconnected' | 'PlayerReconnected'
  | 'SessionCreated' | 'SessionClosed' | 'GameStarted' | 'GameArchived'
  | 'LootGenerated' | 'AITool' | 'RollRequest' | 'RollConfirm' | 'RollDecline'
  | string

export interface Message {
  id: string
  sessionId: string
  playerId: string | null
  content: string
  type: MessageType
  metadata: Record<string, unknown> | null
  isOOC: boolean
  whisperFromId: string | null
  whisperToId: string | null
  whisperTarget: string | null
  isDeleted: boolean
  createdAt: string
  playerDisplayName?: string
}

export interface MessagePage {
  items: Message[]
  hasMore: boolean
  nextCursor: string | null
}
