export type MessageType =
  | 'Player' | 'GM' | 'System' | 'OOC' | 'Whisper' | 'OOCWhisper'
  | 'DiceRoll' | 'SkillCheck' | 'AttackRoll' | 'SpellCast'
  | 'CombatStart' | 'CombatEnd' | 'CombatAttack' | 'CombatDamage'
  | 'CombatHealing' | 'CombatCondition' | 'CombatInitiative' | 'CombatTurn'
  | 'CombatDeath' | 'CombatRevival' | 'CombatDeathSave'
  | 'PlayerJoined' | 'PlayerLeft' | 'PlayerDisconnected' | 'PlayerReconnected'
  | 'SessionCreated' | 'SessionClosed' | 'GameStarted' | 'GameArchived'
  | 'LootGenerated' | 'AITool' | 'RollRequest' | 'RollConfirm' | 'RollDecline'
  // Prompts addressed to one player. They carry their actions in metadata and are replaced
  // in place by their outcome, so a resolved RollRequest becomes a DiceRoll on the same row.
  | 'RerollOffer'
  | string

/**
 * Union of what the history endpoint (MessageHistoryDto) and the live hub (MessageDto)
 * send. The hub payload is the narrower of the two, so the whisper fields are optional.
 */
export interface Message {
  id: string
  sessionId: string
  playerId: string | null
  content: string
  type: MessageType
  metadata: Record<string, unknown> | null
  isOOC: boolean
  createdAt: string
  whisperFromId?: string | null
  whisperToId?: string | null
  whisperTarget?: string | null
  playerDisplayName?: string | null
  /** A roll only the roller and the GM can see. Present on history; the live hub sends it
   *  inside metadata, and only ever to that audience in the first place. */
  isSecret?: boolean
}

export interface MessagePage {
  items: Message[]
  hasMore: boolean
  nextCursor: string | null
}
