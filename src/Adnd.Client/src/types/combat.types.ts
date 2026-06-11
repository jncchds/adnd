export enum CombatStatus {
  Active = 0,
  Paused = 1,
  Finished = 2,
}

export enum CombatEventType {
  CombatStart = 0,
  CombatEnd = 1,
  TurnChange = 2,
  Attack = 3,
  Damage = 4,
  Healing = 5,
  Condition = 6,
  SaveThrow = 7,
  Initiative = 8,
  Death = 9,
  Revival = 10,
  RoundStart = 11,
}

export enum CombatParticipantType {
  Player = 'Player',
  NPC = 'NPC',
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

export interface ConditionEntry {
  name: string;
  duration: number;
  description?: string;
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

export interface CombatAttackResult {
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

export interface CombatSaveThrowResult {
  participant: string;
  saveType: string;
  diceRoll: number;
  dc: number;
  success: boolean;
}

export interface CombatDeathSaveResult {
  participant: string;
  success: boolean;
  successes: number;
  failures: number;
  isStabilized: boolean;
  isDead: boolean;
}

export interface CombatSummary {
  id: string;
  name?: string;
  status: string;
  currentRound: number;
  participantCount: number;
  startedAt: string;
}

export interface InitiativeRoll {
  participantId: string;
  displayName: string;
  initiative: number;
  rolls: number[];
}

export interface SpellCastResult {
  caster: string;
  spellName: string;
  spellLevel: string;
  target: string;
  saveType: string;
  saveDC: number;
  saveSuccess: boolean;
  isCritical: boolean;
  damageType: string;
  damageTotal: number;
  damageInfo: string;
  effect: string;
  targetHP?: number;
  targetMaxHP?: number;
}

export interface LevelUpResult {
  participantId: string;
  newLevel: number;
  systemId: string;
}

export interface RestStatus {
  restType: string;
  isInProgress: boolean;
  roundsRemaining: number;
  hpRecovered: number;
  effects: string[];
}

export interface GridPosition {
  participantId: string;
  gridX: number;
  gridY: number;
  displayName: string;
  moveSpeed: number;
}

export interface AISuggestions {
  combatId: string;
  threatLevel: string;
  recommendedStrategy: string;
  suggestions: AITacticalAction[];
  npcActions: AINPCAction[];
  warnings: AICombatWarning[];
}

export interface AITacticalAction {
  actor: string;
  action: string;
  target: string;
  reason: string;
  priority: number;
  details?: string;
}

export interface AINPCAction {
  npcName: string;
  behavior: string;
  target: string;
  action: string;
  reason: string;
}

export interface AICombatWarning {
  message: string;
  severity: string;
  affectedParticipant?: string;
}

export interface SANCheckResult {
  participant: string;
  currentSAN: number;
  roll: number;
  dc: number;
  success: boolean;
  isCritical: boolean;
  sanLoss: number;
  effect: string;
}
