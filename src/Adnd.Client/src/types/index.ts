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
  Disconnected = 1,
  Left = 2,
}

export enum PlotThreadStatus {
  Active = 0,
  Resolved = 1,
  Abandoned = 2,
}

// Whisper types
export enum WhisperType {
  // === In-game whispers (narrative) ===
  InGamePlayerToGM = 0,
  InGameGMToPlayer = 1,

  // === OOC whispers (non-narrative) ===
  OOCPlayerToGM = 2,
  OOCGMToPlayer = 3,

  // === Legacy (kept for compatibility) ===
  PlayerToPlayer = 4,
  GMToGroup = 5,
  GMToAll = 6,
}

// Agent framework types
export enum AgentType {
  Creator = 0,
  GM = 1,
  LLM = 2,
  Dice = 3,
  RAG = 4,
  NPC = 5,
  Player = 6,
  System = 7,
}

export enum AgentAction {
  Query = 0,
  Generate = 1,
  Roll = 2,
  Check = 3,
  Narrate = 4,
  Suggest = 5,
  Execute = 6,
  Notify = 7,
  Recall = 8,
  ManageState = 9,
  Nudge = 10,
  CreateCharacter = 11,
}

export enum AgentCallStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4,
}

// Combat types
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

// LLM Preset types
export enum LLMProviderType {
  Ollama = 0,
  LmStudio = 1,
  OpenAI = 2,
  Google = 3,
}

export interface LLMPreset {
  id: string;
  name: string;
  providerType: string;
  baseModel: string;
  endpointUrl?: string;
  hasApiKey: boolean;
  temperature: number;
  maxTokens: number;
  topP: number;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface LLMInteractionLog {
  id: string;
  presetId?: string;
  presetName?: string;
  providerType: string;
  model: string;
  promptTokens?: number;
  completionTokens?: number;
  totalTokens?: number;
  durationMs: number;
  success: boolean;
  error?: string;
  systemPrompt?: string;
  userPrompt?: string;
  response?: string;
  requestJson?: string;
  responseJson?: string;
  origin: string;
  originGameId?: string;
  originSessionId?: string;
  originAgent?: string;
  originAction?: string;
  startedAt: string;
  completedAt: string;
}

export interface PresetUsageSummary {
  presetId: string;
  presetName: string;
  providerType: string;
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  totalTokens: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  avgDurationMs: number;
  lastUsed: string;
}

export interface GameProviderUsageSummary {
  providerType: string;
  model: string;
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  successRate: number;
  totalTokens: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  avgDurationMs: number;
  maxDurationMs: number;
  firstCall: string;
  lastCall: string;
}

// Combat interfaces
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

// ============= Spell types =============

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

// ============= Progression types =============

export interface LevelUpResult {
  participantId: string;
  newLevel: number;
  systemId: string;
}

// ============= Rest types =============

export interface RestStatus {
  restType: string;
  isInProgress: boolean;
  roundsRemaining: number;
  hpRecovered: number;
  effects: string[];
}

// ============= Grid types =============

export interface GridPosition {
  participantId: string;
  gridX: number;
  gridY: number;
  displayName: string;
  moveSpeed: number;
}

// ============= AI Combat types =============

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

// ============= SAN types =============

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

// ============= PlotWeaver types =============

export enum PlotThreadCategory {
  General = 0,
  Faction = 1,
  Mystery = 2,
  Personal = 3,
  Threat = 4,
  WorldEvent = 5,
  Relationship = 6,
}

export enum MilestoneStatus {
  Pending = 0,
  Triggered = 1,
  Completed = 2,
  Abandoned = 3,
}

export enum OpportunityType {
  NewThread = 0,
  SpawnMilestone = 1,
  AdaptThread = 2,
  MergeThreads = 3,
  EscalateThreat = 4,
}

export interface PlotThreadResponse {
  id: string;
  title: string;
  category: PlotThreadCategory;
  description: string;
  status: PlotThreadStatus;
  momentum: number;
  relevanceScore: number;
  nextMilestone: string | null;
  foreshadowing: string | null;
  adaptationHistory: string[];
  createdAt: string;
  updatedAt: string | null;
}

export interface ThreadUpdate {
  threadId: string;
  threadTitle: string;
  oldMomentum: number | null;
  newMomentum: number | null;
  oldStatus: string | null;
  newStatus: string | null;
  oldDescription: string | null;
  newDescription: string | null;
  oldMilestone: string | null;
  newMilestone: string | null;
  reason: string;
}

export interface PlotReviewResponse {
  id: string;
  trigger: string;
  summary: string;
  updates: ThreadUpdate[];
  reviewedAt: string;
}

export interface AdjustMomentumRequest {
  delta: number;
  reason: string;
}

// ============= Game Template types =============

export interface GameTemplate {
  id: string;
  name: string;
  defaultName?: string;
  systemId: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language: string;
  plotSeed?: string;
  gameParameters?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateGameTemplateRequest {
  name: string;
  defaultName?: string;
  systemId: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language: string;
  plotSeed?: string;
  gameParameters?: string;
}

export interface UpdateGameTemplateRequest {
  name: string;
  defaultName?: string;
  systemId: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language: string;
  plotSeed?: string;
  gameParameters?: string;
}
