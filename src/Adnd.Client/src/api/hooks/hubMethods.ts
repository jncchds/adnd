import { HubConnection } from '@microsoft/signalr';

export interface HubMethods {
  sendMessage: (sessionId: string, content: string) => any;
  sendInGameWhisper: (sessionId: string, content: string) => any;
  sendOOCMessage: (sessionId: string, content: string) => any;
  sendOOCWhisper: (sessionId: string, content: string) => any;
  sendOOCWhisperToPlayer: (targetPlayerId: string, content: string) => any;
  sendInGameWhisperToPlayer: (targetPlayerId: string, content: string) => any;
  sendWhisper: (targets: string, content: string) => any;
  sendGMWhisper: (targetPlayerId: string, content: string) => any;
  callAgent: (fromAgent: number, toAgent: number, action: number, input: string, sessionId?: string) => any;
  getWhisperHistory: (gameId: string, limit?: number) => any;
  getAgentCallHistory: (gameId: string, fromAgent?: number, action?: number, limit?: number) => any;
  getAgentCall: (callId: string) => any;
  triggerNarrate: (gameId: string) => any;
  triggerSuggest: (gameId: string) => any;
  triggerConsistency: (gameId: string) => any;
  triggerReview: (gameId: string, context?: string) => any;
  triggerFullReview: (gameId: string, context?: string) => any;
  triggerNewScene: (gameId: string) => any;
  triggerGMEvaluate: (gameId: string) => any;
  triggerPlotCheck: (gameId: string) => any;
  triggerDetectOpportunities: (gameId: string) => any;
  triggerGenerateThreads: (gameId: string) => any;
  triggerSpawnMilestones: (gameId: string) => any;
  triggerSessionSummary: (gameId: string, sessionId?: string) => any;
  triggerPause: (gameId: string) => any;
  triggerResume: (gameId: string) => any;
  triggerCombatStart: (gameId: string) => any;
  triggerCombatEnd: (gameId: string) => any;
  rollDice: (gameId: string, formula: string, metadata?: Record<string, any>) => any;
  startSkillCheck: (gameId: string, skill: string, metadata?: Record<string, any>) => any;
  startAttack: (gameId: string, targetId: string, weaponName: string, metadata?: Record<string, any>) => any;
  startSpellCast: (gameId: string, spellName: string, targetId: string, metadata?: Record<string, any>) => any;
  getGameState: (gameId: string) => any;
  updateGameState: (gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) => any;
  getPlotWeaverThreads: (gameId: string) => any;
  triggerPlotReview: (gameId: string, context?: string) => any;
  getPlotReviewHistory: (gameId: string, limit?: number) => any;
  adjustThreadMomentum: (gameId: string, threadId: string, delta: number, reason: string) => any;
  detectOpportunities: (gameId: string) => any;
  getSwayStatus: (gameId: string) => any;
  sendSway: (gameId: string, direction: string) => any;
  getActiveCombats: (gameId: string) => any;
  startCombat: (gameId: string) => any;
  endCombat: (gameId: string) => any;
  pauseCombat: (gameId: string) => any;
  resumeCombat: (gameId: string) => any;
  getCombat: (gameId: string, combatId: string) => any;
  getCombatLog: (gameId: string, combatId: string) => any;
  getPlayers: (gameId: string) => any;
  getNPCs: (gameId: string) => any;
  getPlotThreads: (gameId: string) => any;
  getCharacters: (gameId: string) => any;
  getCharacter: (characterId: string) => any;
  getLLMPresets: () => any;
  getLLMPreset: (presetId: string) => any;
  createLLMPreset: (request: any) => any;
  updateLLMPreset: (presetId: string, request: any) => any;
  deleteLLMPreset: (presetId: string) => any;
  testLLMPreset: (presetId: string) => any;
  setDefaultPreset: (presetId: string) => any;
  getLLMProviders: () => any;
  getPlotContext: (gameId: string, maxMessages?: number) => any;
  findSimilarThreads: (gameId: string, query: string, limit?: number) => any;
  checkConsistency: (gameId: string, messageCount?: number) => any;
  getSessionSummary: (gameId: string, sessionId: string, messageCount?: number) => any;
  getSystems: () => any;
  getLLMInteractions: (params?: any) => any;
  getLLMInteraction: (logId: string) => any;
  deleteLLMInteraction: (logId: string) => any;
  getPresetUsage: (from?: string, to?: string) => any;
  getGameProviderUsage: (gameId: string, from?: string, to?: string) => any;
  cleanupOldLogs: (before: string) => any;
  getPendingToolCalls: (gameId: string) => any;
  confirmToolCall: (gameId: string, toolCallId: string, approved: boolean) => any;
  confirmPlayerRoll: (gameId: string, toolCallId: string) => any;
  declinePlayerRoll: (gameId: string, toolCallId: string) => any;
  getDiceHistory: (gameId: string, params?: any) => any;
  getDiceRoll: (gameId: string, messageId: string) => any;
  getDiceStats: (gameId: string, sessionId?: string) => any;
  getPlayerDiceStats: (gameId: string, playerId: string, sessionId?: string) => any;
  getMessagesPaginated: (gameId: string, sessionId: string, page?: number, pageSize?: number, type?: number, anchorId?: string) => any;
  searchMessages: (gameId: string, sessionId: string, query: string, queryEmbedding: number[], limit?: number) => any;
  getPromptTemplates: (gameId: string) => any;
  createPromptTemplate: (gameId: string, name: string, type: string, prompt: string) => any;
  updatePromptTemplate: (gameId: string, templateId: string, name: string, type: string, prompt: string, isActive?: boolean, isDefault?: boolean) => any;
  deletePromptTemplate: (gameId: string, templateId: string) => any;
  getDefaultTemplate: (gameId: string, type: string) => any;
  getGameTemplates: () => any;
  createGameTemplate: (request: any) => any;
  updateGameTemplate: (id: string, request: any) => any;
  deleteGameTemplate: (id: string) => any;
  getGameTemplate: (id: string) => any;
  getProviderModels: (providerType: string, endpointUrl?: string, apiKey?: string) => any;
  getGameStatePage: (gameId: string) => any;
  updateGameStatePage: (gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) => any;
  getGMTools: (gameId: string) => any;
  getGMToolsByCategory: (gameId: string, category: string) => any;
  executeGMTool: (gameId: string, sessionId: string, request: any) => any;
  getCharacterSpells: (characterId: string) => any;
  updateCharacterSpells: (characterId: string, spells: any[]) => any;
  updateSingleSpell: (characterId: string, spellName: string, spell: any) => any;
  removeSpell: (characterId: string, spellName: string) => any;
  getSessionNotes: (gameId: string, sessionId: string) => any;
  createSessionNote: (gameId: string, sessionId: string, title: string, content: string) => any;
  updateSessionNote: (gameId: string, noteId: string, title: string, content: string) => any;
  deleteSessionNote: (gameId: string, noteId: string) => any;
}

export function createHubMethods(hubRef: React.MutableRefObject<HubConnection | null>): HubMethods {
  const invoke = (method: string, ...args: any[]) => hubRef.current?.invoke(method, ...args);

  return {
    // Chat
    sendMessage: (sessionId: string, content: string) => invoke('SendMessage', sessionId, content),
    sendInGameWhisper: (sessionId: string, content: string) => invoke('SendInGameWhisper', sessionId, content),
    sendOOCMessage: (sessionId: string, content: string) => invoke('SendOOCMessage', sessionId, content),
    sendOOCWhisper: (sessionId: string, content: string) => invoke('SendOOCWhisper', sessionId, content),
    sendOOCWhisperToPlayer: (targetPlayerId: string, content: string) => invoke('SendOOCWhisperToPlayer', targetPlayerId, content),
    sendInGameWhisperToPlayer: (targetPlayerId: string, content: string) => invoke('SendInGameWhisperToPlayer', targetPlayerId, content),
    sendWhisper: (targets: string, content: string) => invoke('SendWhisper', targets, content),
    sendGMWhisper: (targetPlayerId: string, content: string) => invoke('SendGMWhisper', targetPlayerId, content),

    // Agent
    callAgent: (fromAgent: number, toAgent: number, action: number, input: string, sessionId?: string) => invoke('CallAgent', fromAgent, toAgent, action, input, sessionId),
    getWhisperHistory: (gameId: string, limit = 50) => invoke('GetWhisperHistory', gameId, limit),
    getAgentCallHistory: (gameId: string, fromAgent?: number, action?: number, limit = 50) => invoke('GetAgentCallHistory', gameId, fromAgent, action, limit),
    getAgentCall: (callId: string) => invoke('GetAgentCall', callId),

    // Manual Triggers
    triggerNarrate: (gameId: string) => invoke('TriggerNarrate', gameId),
    triggerSuggest: (gameId: string) => invoke('TriggerSuggest', gameId),
    triggerConsistency: (gameId: string) => invoke('TriggerConsistency', gameId),
    triggerReview: (gameId: string, context?: string) => invoke('TriggerReview', gameId, context),
    triggerFullReview: (gameId: string, context?: string) => invoke('TriggerFullReview', gameId, context),
    triggerNewScene: (gameId: string) => invoke('TriggerNewScene', gameId),
    triggerGMEvaluate: (gameId: string) => invoke('TriggerGMEvaluate', gameId),
    triggerPlotCheck: (gameId: string) => invoke('TriggerPlotCheck', gameId),
    triggerDetectOpportunities: (gameId: string) => invoke('TriggerDetectOpportunities', gameId),
    triggerGenerateThreads: (gameId: string) => invoke('TriggerGenerateThreads', gameId),
    triggerSpawnMilestones: (gameId: string) => invoke('TriggerSpawnMilestones', gameId),
    triggerSessionSummary: (gameId: string, sessionId?: string) => invoke('TriggerSessionSummary', gameId, sessionId),
    triggerPause: (gameId: string) => invoke('TriggerPause', gameId),
    triggerResume: (gameId: string) => invoke('TriggerResume', gameId),
    triggerCombatStart: (gameId: string) => invoke('TriggerCombatStart', gameId),
    triggerCombatEnd: (gameId: string) => invoke('TriggerCombatEnd', gameId),

    // Dice & Actions
    rollDice: (gameId: string, formula: string, metadata?: Record<string, any>) => invoke('RollDice', gameId, formula, metadata),
    startSkillCheck: (gameId: string, skill: string, metadata?: Record<string, any>) => invoke('StartSkillCheck', gameId, skill, metadata),
    startAttack: (gameId: string, targetId: string, weaponName: string, metadata?: Record<string, any>) => invoke('StartAttack', gameId, targetId, weaponName, metadata),
    startSpellCast: (gameId: string, spellName: string, targetId: string, metadata?: Record<string, any>) => invoke('StartSpellCast', gameId, spellName, targetId, metadata),

    // Game State
    getGameState: (gameId: string) => invoke('GetGameState', gameId),
    updateGameState: (gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) => invoke('UpdateGameState', gameId, gameState, plotSeed, gameParameters),

    // Plot
    getPlotWeaverThreads: (gameId: string) => invoke('GetPlotWeaverThreads', gameId),
    triggerPlotReview: (gameId: string, context?: string) => invoke('TriggerPlotReview', gameId, context),
    getPlotReviewHistory: (gameId: string, limit = 20) => invoke('GetPlotReviewHistory', gameId, limit),
    adjustThreadMomentum: (gameId: string, threadId: string, delta: number, reason: string) => invoke('AdjustThreadMomentum', gameId, threadId, delta, reason),
    detectOpportunities: (gameId: string) => invoke('DetectOpportunities', gameId),
    getSwayStatus: (gameId: string) => invoke('GetSwayStatus', gameId),
    sendSway: (gameId: string, direction: string) => invoke('SendSway', gameId, direction),

    // Combat
    getActiveCombats: (gameId: string) => invoke('GetActiveCombats', gameId),
    startCombat: (gameId: string) => invoke('StartCombat', gameId),
    endCombat: (gameId: string) => invoke('EndCombat', gameId),
    pauseCombat: (gameId: string) => invoke('PauseCombat', gameId),
    resumeCombat: (gameId: string) => invoke('ResumeCombat', gameId),
    getCombat: (gameId: string, combatId: string) => invoke('GetCombat', gameId, combatId),
    getCombatLog: (gameId: string, combatId: string) => invoke('GetCombatLog', gameId, combatId),

    // Admin
    getPlayers: (gameId: string) => invoke('GetPlayers', gameId),
    getNPCs: (gameId: string) => invoke('GetNPCs', gameId),
    getPlotThreads: (gameId: string) => invoke('GetPlotThreads', gameId),
    getCharacters: (gameId: string) => invoke('GetCharacters', gameId),
    getCharacter: (characterId: string) => invoke('GetCharacter', characterId),

    // LLM
    getLLMPresets: () => invoke('GetLLMPresets'),
    getLLMPreset: (presetId: string) => invoke('GetLLMPreset', presetId),
    createLLMPreset: (request: any) => invoke('CreateLLMPreset', request),
    updateLLMPreset: (presetId: string, request: any) => invoke('UpdateLLMPreset', presetId, request),
    deleteLLMPreset: (presetId: string) => invoke('DeleteLLMPreset', presetId),
    testLLMPreset: (presetId: string) => invoke('TestLLMPreset', presetId),
    setDefaultPreset: (presetId: string) => invoke('SetDefaultPreset', presetId),
    getLLMProviders: () => invoke('GetLLMProviders'),
    getPlotContext: (gameId: string, maxMessages = 20) => invoke('GetPlotContext', gameId, maxMessages),
    findSimilarThreads: (gameId: string, query: string, limit = 5) => invoke('FindSimilarThreads', gameId, query, limit),
    checkConsistency: (gameId: string, messageCount = 50) => invoke('CheckConsistency', gameId, messageCount),
    getSessionSummary: (gameId: string, sessionId: string, messageCount = 30) => invoke('GetSessionSummary', gameId, sessionId, messageCount),
    getSystems: () => invoke('GetSystems'),
    getLLMInteractions: (params?: any) => invoke('GetLLMInteractions', params),
    getLLMInteraction: (logId: string) => invoke('GetLLMInteraction', logId),
    deleteLLMInteraction: (logId: string) => invoke('DeleteLLMInteraction', logId),
    getPresetUsage: (from?: string, to?: string) => invoke('GetPresetUsage', from, to),
    getGameProviderUsage: (gameId: string, from?: string, to?: string) => invoke('GetGameProviderUsage', gameId, from, to),
    cleanupOldLogs: (before: string) => invoke('CleanupOldLogs', before),

    // Tool Calls
    getPendingToolCalls: (gameId: string) => invoke('GetPendingToolCalls', gameId),
    confirmToolCall: (gameId: string, toolCallId: string, approved: boolean) => invoke('ConfirmToolCall', gameId, toolCallId, approved),
    confirmPlayerRoll: (gameId: string, toolCallId: string) => invoke('ConfirmPlayerRoll', gameId, toolCallId),
    declinePlayerRoll: (gameId: string, toolCallId: string) => invoke('DeclinePlayerRoll', gameId, toolCallId),

    // Dice
    getDiceHistory: (gameId: string, params?: any) => invoke('GetDiceHistory', gameId, params),
    getDiceRoll: (gameId: string, messageId: string) => invoke('GetDiceRoll', gameId, messageId),
    getDiceStats: (gameId: string, sessionId?: string) => invoke('GetDiceStats', gameId, sessionId),
    getPlayerDiceStats: (gameId: string, playerId: string, sessionId?: string) => invoke('GetPlayerDiceStats', gameId, playerId, sessionId),

    // Messages
    getMessagesPaginated: (gameId: string, sessionId: string, page = 1, pageSize = 50, type?: number, anchorId?: string) => invoke('GetMessagesPaginated', gameId, sessionId, page, pageSize, type, anchorId),
    searchMessages: (gameId: string, sessionId: string, query: string, queryEmbedding: number[], limit = 10) => invoke('SearchMessages', gameId, sessionId, query, queryEmbedding, limit),

    // Prompt Templates
    getPromptTemplates: (gameId: string) => invoke('GetPromptTemplates', gameId),
    createPromptTemplate: (gameId: string, name: string, type: string, prompt: string) => invoke('CreatePromptTemplate', gameId, name, type, prompt),
    updatePromptTemplate: (gameId: string, templateId: string, name: string, type: string, prompt: string, isActive?: boolean, isDefault?: boolean) => invoke('UpdatePromptTemplate', gameId, templateId, name, type, prompt, isActive, isDefault),
    deletePromptTemplate: (gameId: string, templateId: string) => invoke('DeletePromptTemplate', gameId, templateId),
    getDefaultTemplate: (gameId: string, type: string) => invoke('GetDefaultTemplate', gameId, type),

    // Game Templates
    getGameTemplates: () => invoke('GetGameTemplates'),
    createGameTemplate: (request: any) => invoke('CreateGameTemplate', request),
    updateGameTemplate: (id: string, request: any) => invoke('UpdateGameTemplate', id, request),
    deleteGameTemplate: (id: string) => invoke('DeleteGameTemplate', id),
    getGameTemplate: (id: string) => invoke('GetGameTemplate', id),

    // Provider
    getProviderModels: (providerType: string, endpointUrl?: string, apiKey?: string) => invoke('GetProviderModels', providerType, endpointUrl, apiKey),

    // Game State Page
    getGameStatePage: (gameId: string) => invoke('GetGameStatePage', gameId),
    updateGameStatePage: (gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) => invoke('UpdateGameStatePage', gameId, gameState, plotSeed, gameParameters),

    // GM Tools
    getGMTools: (gameId: string) => invoke('GetGMTools', gameId),
    getGMToolsByCategory: (gameId: string, category: string) => invoke('GetGMToolsByCategory', gameId, category),
    executeGMTool: (gameId: string, sessionId: string, request: any) => invoke('ExecuteGMTool', gameId, sessionId, request),

    // Spells
    getCharacterSpells: (characterId: string) => invoke('GetCharacterSpells', characterId),
    updateCharacterSpells: (characterId: string, spells: any[]) => invoke('UpdateCharacterSpells', characterId, spells),
    updateSingleSpell: (characterId: string, spellName: string, spell: any) => invoke('UpdateSingleSpell', characterId, spellName, spell),
    removeSpell: (characterId: string, spellName: string) => invoke('RemoveSpell', characterId, spellName),

    // Session Notes
    getSessionNotes: (gameId: string, sessionId: string) => invoke('GetSessionNotes', gameId, sessionId),
    createSessionNote: (gameId: string, sessionId: string, title: string, content: string) => invoke('CreateSessionNote', gameId, sessionId, title, content),
    updateSessionNote: (gameId: string, noteId: string, title: string, content: string) => invoke('UpdateSessionNote', gameId, noteId, title, content),
    deleteSessionNote: (gameId: string, noteId: string) => invoke('DeleteSessionNote', gameId, noteId),
  };
}
