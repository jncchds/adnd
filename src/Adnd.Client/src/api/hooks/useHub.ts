import { useState, useCallback, useRef } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

// ==================== Hub Connection ====================

// ==================== Hub Hook ====================

export function useGameHub() {
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const hubRef = useRef<HubConnection | null>(null);

  const connect = useCallback(async (gameId: string, token: string) => {
    if (!hubRef.current) {
      hubRef.current = new HubConnectionBuilder()
        .withUrl(`/api/games/${gameId}/gamehub`, {
          accessTokenFactory: () => Promise.resolve(token),
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.None)
        .build();
    }

    try {
      await hubRef.current.start();
      await hubRef.current.invoke('JoinGameGroup', gameId);
      setIsConnected(true);
      setError(null);
    } catch (e: any) {
      setError(`Failed to connect: ${e.message}`);
      setIsConnected(false);
    }
  }, []);

  const disconnect = useCallback(async () => {
    if (hubRef.current) {
      await hubRef.current.stop();
      hubRef.current = null;
      setIsConnected(false);
    }
  }, []);

  const on = useCallback((event: string, handler: (...args: any[]) => void) => {
    if (hubRef.current) {
      hubRef.current.on(event, handler);
    }
  }, []);

  const off = useCallback((event: string, handler?: ((...args: any[]) => void) | undefined) => {
    if (hubRef.current && handler) {
      hubRef.current.off(event, handler);
    } else if (hubRef.current) {
      hubRef.current.off(event);
    }
  }, []);

  const invoke = useCallback(async (method: string, ...args: any[]) => {
    if (!hubRef.current) {
      throw new Error('Hub connection not established');
    }
    return hubRef.current.invoke(method, ...args);
  }, []);

  const waitForConnection = useCallback(async () => {
    if (isConnected) return true;
    return new Promise<boolean>((resolve) => {
      const check = setInterval(() => {
        if (isConnected) {
          clearInterval(check);
          resolve(true);
        }
      }, 100);
      setTimeout(() => {
        clearInterval(check);
        resolve(false);
      }, 10000);
    });
  }, [isConnected]);

  // Hub methods
  const sendMessage = useCallback((sessionId: string, content: string) => {
    return hubRef.current?.invoke('SendMessage', sessionId, content);
  }, []);

  const sendInGameWhisper = useCallback((sessionId: string, content: string) => {
    return hubRef.current?.invoke('SendInGameWhisper', sessionId, content);
  }, []);

  const sendOOCMessage = useCallback((sessionId: string, content: string) => {
    return hubRef.current?.invoke('SendOOCMessage', sessionId, content);
  }, []);

  const sendOOCWhisper = useCallback((sessionId: string, content: string) => {
    return hubRef.current?.invoke('SendOOCWhisper', sessionId, content);
  }, []);

  const sendOOCWhisperToPlayer = useCallback((targetPlayerId: string, content: string) => {
    return hubRef.current?.invoke('SendOOCWhisperToPlayer', targetPlayerId, content);
  }, []);

  const sendInGameWhisperToPlayer = useCallback((targetPlayerId: string, content: string) => {
    return hubRef.current?.invoke('SendInGameWhisperToPlayer', targetPlayerId, content);
  }, []);

  const sendWhisper = useCallback((targets: string, content: string) => {
    return hubRef.current?.invoke('SendWhisper', targets, content);
  }, []);

  const sendGMWhisper = useCallback((targetPlayerId: string, content: string) => {
    return hubRef.current?.invoke('SendGMWhisper', targetPlayerId, content);
  }, []);

  const callAgent = useCallback((fromAgent: number, toAgent: number, action: number, input: string, sessionId?: string) => {
    return hubRef.current?.invoke('CallAgent', fromAgent, toAgent, action, input, sessionId);
  }, []);

  const getWhisperHistory = useCallback((gameId: string, limit = 50) => {
    return hubRef.current?.invoke('GetWhisperHistory', gameId, limit);
  }, []);

  const getAgentCallHistory = useCallback((gameId: string, fromAgent?: number, action?: number, limit = 50) => {
    return hubRef.current?.invoke('GetAgentCallHistory', gameId, fromAgent, action, limit);
  }, []);

  const getAgentCall = useCallback((callId: string) => {
    return hubRef.current?.invoke('GetAgentCall', callId);
  }, []);

  const triggerNarrate = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerNarrate', gameId);
  }, []);

  const triggerSuggest = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerSuggest', gameId);
  }, []);

  const triggerConsistency = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerConsistency', gameId);
  }, []);

  const triggerReview = useCallback((gameId: string, context?: string) => {
    return hubRef.current?.invoke('TriggerReview', gameId, context);
  }, []);

  const triggerFullReview = useCallback((gameId: string, context?: string) => {
    return hubRef.current?.invoke('TriggerFullReview', gameId, context);
  }, []);

  const triggerNewScene = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerNewScene', gameId);
  }, []);

  const triggerGMEvaluate = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerGMEvaluate', gameId);
  }, []);

  const triggerPlotCheck = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerPlotCheck', gameId);
  }, []);

  const triggerDetectOpportunities = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerDetectOpportunities', gameId);
  }, []);

  const triggerGenerateThreads = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerGenerateThreads', gameId);
  }, []);

  const triggerSpawnMilestones = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerSpawnMilestones', gameId);
  }, []);

  const triggerSessionSummary = useCallback((gameId: string, sessionId?: string) => {
    return hubRef.current?.invoke('TriggerSessionSummary', gameId, sessionId);
  }, []);

  const triggerPause = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerPause', gameId);
  }, []);

  const triggerResume = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerResume', gameId);
  }, []);

  const triggerCombatStart = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerCombatStart', gameId);
  }, []);

  const triggerCombatEnd = useCallback((gameId: string) => {
    return hubRef.current?.invoke('TriggerCombatEnd', gameId);
  }, []);

  const rollDice = useCallback((gameId: string, formula: string, metadata?: Record<string, any>) => {
    return hubRef.current?.invoke('RollDice', gameId, formula, metadata);
  }, []);

  const startSkillCheck = useCallback((gameId: string, skill: string, metadata?: Record<string, any>) => {
    return hubRef.current?.invoke('StartSkillCheck', gameId, skill, metadata);
  }, []);

  const startAttack = useCallback((gameId: string, targetId: string, weaponName: string, metadata?: Record<string, any>) => {
    return hubRef.current?.invoke('StartAttack', gameId, targetId, weaponName, metadata);
  }, []);

  const startSpellCast = useCallback((gameId: string, spellName: string, targetId: string, metadata?: Record<string, any>) => {
    return hubRef.current?.invoke('StartSpellCast', gameId, spellName, targetId, metadata);
  }, []);

  const getGameState = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetGameState', gameId);
  }, []);

  const updateGameState = useCallback((gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) => {
    return hubRef.current?.invoke('UpdateGameState', gameId, gameState, plotSeed, gameParameters);
  }, []);

  const getPlotWeaverThreads = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetPlotWeaverThreads', gameId);
  }, []);

  const triggerPlotReview = useCallback((gameId: string, context?: string) => {
    return hubRef.current?.invoke('TriggerPlotReview', gameId, context);
  }, []);

  const getPlotReviewHistory = useCallback((gameId: string, limit = 20) => {
    return hubRef.current?.invoke('GetPlotReviewHistory', gameId, limit);
  }, []);

  const adjustThreadMomentum = useCallback((gameId: string, threadId: string, delta: number, reason: string) => {
    return hubRef.current?.invoke('AdjustThreadMomentum', gameId, threadId, delta, reason);
  }, []);

  const detectOpportunities = useCallback((gameId: string) => {
    return hubRef.current?.invoke('DetectOpportunities', gameId);
  }, []);

  const getSwayStatus = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetSwayStatus', gameId);
  }, []);

  const sendSway = useCallback((gameId: string, direction: string) => {
    return hubRef.current?.invoke('SendSway', gameId, direction);
  }, []);

  const getActiveCombats = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetActiveCombats', gameId);
  }, []);

  const startCombat = useCallback((gameId: string) => {
    return hubRef.current?.invoke('StartCombat', gameId);
  }, []);

  const endCombat = useCallback((gameId: string) => {
    return hubRef.current?.invoke('EndCombat', gameId);
  }, []);

  const pauseCombat = useCallback((gameId: string) => {
    return hubRef.current?.invoke('PauseCombat', gameId);
  }, []);

  const resumeCombat = useCallback((gameId: string) => {
    return hubRef.current?.invoke('ResumeCombat', gameId);
  }, []);

  const getCombat = useCallback((gameId: string, combatId: string) => {
    return hubRef.current?.invoke('GetCombat', gameId, combatId);
  }, []);

  const getCombatLog = useCallback((gameId: string, combatId: string) => {
    return hubRef.current?.invoke('GetCombatLog', gameId, combatId);
  }, []);

  const getPlayers = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetPlayers', gameId);
  }, []);

  const getNPCs = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetNPCs', gameId);
  }, []);

  const getPlotThreads = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetPlotThreads', gameId);
  }, []);

  const getCharacters = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetCharacters', gameId);
  }, []);

  const getCharacter = useCallback((characterId: string) => {
    return hubRef.current?.invoke('GetCharacter', characterId);
  }, []);

  const getLLMPresets = useCallback(() => {
    return hubRef.current?.invoke('GetLLMPresets');
  }, []);

  const getLLMPreset = useCallback((presetId: string) => {
    return hubRef.current?.invoke('GetLLMPreset', presetId);
  }, []);

  const createLLMPreset = useCallback((request: any) => {
    return hubRef.current?.invoke('CreateLLMPreset', request);
  }, []);

  const updateLLMPreset = useCallback((presetId: string, request: any) => {
    return hubRef.current?.invoke('UpdateLLMPreset', presetId, request);
  }, []);

  const deleteLLMPreset = useCallback((presetId: string) => {
    return hubRef.current?.invoke('DeleteLLMPreset', presetId);
  }, []);

  const testLLMPreset = useCallback((presetId: string) => {
    return hubRef.current?.invoke('TestLLMPreset', presetId);
  }, []);

  const setDefaultPreset = useCallback((presetId: string) => {
    return hubRef.current?.invoke('SetDefaultPreset', presetId);
  }, []);

  const getLLMProviders = useCallback(() => {
    return hubRef.current?.invoke('GetLLMProviders');
  }, []);

  const getPlotContext = useCallback((gameId: string, maxMessages = 20) => {
    return hubRef.current?.invoke('GetPlotContext', gameId, maxMessages);
  }, []);

  const findSimilarThreads = useCallback((gameId: string, query: string, limit = 5) => {
    return hubRef.current?.invoke('FindSimilarThreads', gameId, query, limit);
  }, []);

  const checkConsistency = useCallback((gameId: string, messageCount = 50) => {
    return hubRef.current?.invoke('CheckConsistency', gameId, messageCount);
  }, []);

  const getSessionSummary = useCallback((gameId: string, sessionId: string, messageCount = 30) => {
    return hubRef.current?.invoke('GetSessionSummary', gameId, sessionId, messageCount);
  }, []);

  const getSystems = useCallback(() => {
    return hubRef.current?.invoke('GetSystems');
  }, []);

  const getLLMInteractions = useCallback((params?: any) => {
    return hubRef.current?.invoke('GetLLMInteractions', params);
  }, []);

  const getLLMInteraction = useCallback((logId: string) => {
    return hubRef.current?.invoke('GetLLMInteraction', logId);
  }, []);

  const deleteLLMInteraction = useCallback((logId: string) => {
    return hubRef.current?.invoke('DeleteLLMInteraction', logId);
  }, []);

  const getPresetUsage = useCallback((from?: string, to?: string) => {
    return hubRef.current?.invoke('GetPresetUsage', from, to);
  }, []);

  const getGameProviderUsage = useCallback((gameId: string, from?: string, to?: string) => {
    return hubRef.current?.invoke('GetGameProviderUsage', gameId, from, to);
  }, []);

  const cleanupOldLogs = useCallback((before: string) => {
    return hubRef.current?.invoke('CleanupOldLogs', before);
  }, []);

  const getPendingToolCalls = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetPendingToolCalls', gameId);
  }, []);

  const confirmToolCall = useCallback((gameId: string, toolCallId: string, approved: boolean) => {
    return hubRef.current?.invoke('ConfirmToolCall', gameId, toolCallId, approved);
  }, []);

  const confirmPlayerRoll = useCallback((gameId: string, toolCallId: string) => {
    return hubRef.current?.invoke('ConfirmPlayerRoll', gameId, toolCallId);
  }, []);

  const declinePlayerRoll = useCallback((gameId: string, toolCallId: string) => {
    return hubRef.current?.invoke('DeclinePlayerRoll', gameId, toolCallId);
  }, []);

  const getDiceHistory = useCallback((gameId: string, params?: any) => {
    return hubRef.current?.invoke('GetDiceHistory', gameId, params);
  }, []);

  const getDiceRoll = useCallback((gameId: string, messageId: string) => {
    return hubRef.current?.invoke('GetDiceRoll', gameId, messageId);
  }, []);

  const getDiceStats = useCallback((gameId: string, sessionId?: string) => {
    return hubRef.current?.invoke('GetDiceStats', gameId, sessionId);
  }, []);

  const getPlayerDiceStats = useCallback((gameId: string, playerId: string, sessionId?: string) => {
    return hubRef.current?.invoke('GetPlayerDiceStats', gameId, playerId, sessionId);
  }, []);

  const getMessagesPaginated = useCallback((gameId: string, sessionId: string, page = 1, pageSize = 50, type?: number, anchorId?: string) => {
    return hubRef.current?.invoke('GetMessagesPaginated', gameId, sessionId, page, pageSize, type, anchorId);
  }, []);

  const searchMessages = useCallback((gameId: string, sessionId: string, query: string, queryEmbedding: number[], limit = 10) => {
    return hubRef.current?.invoke('SearchMessages', gameId, sessionId, query, queryEmbedding, limit);
  }, []);

  const getPromptTemplates = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetPromptTemplates', gameId);
  }, []);

  const createPromptTemplate = useCallback((gameId: string, name: string, type: string, prompt: string) => {
    return hubRef.current?.invoke('CreatePromptTemplate', gameId, name, type, prompt);
  }, []);

  const updatePromptTemplate = useCallback((gameId: string, templateId: string, name: string, type: string, prompt: string, isActive?: boolean, isDefault?: boolean) => {
    return hubRef.current?.invoke('UpdatePromptTemplate', gameId, templateId, name, type, prompt, isActive, isDefault);
  }, []);

  const deletePromptTemplate = useCallback((gameId: string, templateId: string) => {
    return hubRef.current?.invoke('DeletePromptTemplate', gameId, templateId);
  }, []);

  const getDefaultTemplate = useCallback((gameId: string, type: string) => {
    return hubRef.current?.invoke('GetDefaultTemplate', gameId, type);
  }, []);

  const getGameTemplates = useCallback(() => {
    return hubRef.current?.invoke('GetGameTemplates');
  }, []);

  const createGameTemplate = useCallback((request: any) => {
    return hubRef.current?.invoke('CreateGameTemplate', request);
  }, []);

  const updateGameTemplate = useCallback((id: string, request: any) => {
    return hubRef.current?.invoke('UpdateGameTemplate', id, request);
  }, []);

  const deleteGameTemplate = useCallback((id: string) => {
    return hubRef.current?.invoke('DeleteGameTemplate', id);
  }, []);

  const getGameTemplate = useCallback((id: string) => {
    return hubRef.current?.invoke('GetGameTemplate', id);
  }, []);

  const getProviderModels = useCallback((providerType: string, endpointUrl?: string, apiKey?: string) => {
    return hubRef.current?.invoke('GetProviderModels', providerType, endpointUrl, apiKey);
  }, []);

  const getGameStatePage = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetGameStatePage', gameId);
  }, []);

  const updateGameStatePage = useCallback((gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) => {
    return hubRef.current?.invoke('UpdateGameStatePage', gameId, gameState, plotSeed, gameParameters);
  }, []);

  const getGMTools = useCallback((gameId: string) => {
    return hubRef.current?.invoke('GetGMTools', gameId);
  }, []);

  const getGMToolsByCategory = useCallback((gameId: string, category: string) => {
    return hubRef.current?.invoke('GetGMToolsByCategory', gameId, category);
  }, []);

  const executeGMTool = useCallback((gameId: string, sessionId: string, request: any) => {
    return hubRef.current?.invoke('ExecuteGMTool', gameId, sessionId, request);
  }, []);

  const getCharacterSpells = useCallback((characterId: string) => {
    return hubRef.current?.invoke('GetCharacterSpells', characterId);
  }, []);

  const updateCharacterSpells = useCallback((characterId: string, spells: any[]) => {
    return hubRef.current?.invoke('UpdateCharacterSpells', characterId, spells);
  }, []);

  const updateSingleSpell = useCallback((characterId: string, spellName: string, spell: any) => {
    return hubRef.current?.invoke('UpdateSingleSpell', characterId, spellName, spell);
  }, []);

  const removeSpell = useCallback((characterId: string, spellName: string) => {
    return hubRef.current?.invoke('RemoveSpell', characterId, spellName);
  }, []);

  const getSessionNotes = useCallback((gameId: string, sessionId: string) => {
    return hubRef.current?.invoke('GetSessionNotes', gameId, sessionId);
  }, []);

  const createSessionNote = useCallback((gameId: string, sessionId: string, title: string, content: string) => {
    return hubRef.current?.invoke('CreateSessionNote', gameId, sessionId, title, content);
  }, []);

  const updateSessionNote = useCallback((gameId: string, noteId: string, title: string, content: string) => {
    return hubRef.current?.invoke('UpdateSessionNote', gameId, noteId, title, content);
  }, []);

  const deleteSessionNote = useCallback((gameId: string, noteId: string) => {
    return hubRef.current?.invoke('DeleteSessionNote', gameId, noteId);
  }, []);

  return {
    isConnected,
    error,
    connect,
    disconnect,
    on,
    off,
    invoke,
    waitForConnection,
    sendMessage,
    sendInGameWhisper,
    sendOOCMessage,
    sendOOCWhisper,
    sendOOCWhisperToPlayer,
    sendInGameWhisperToPlayer,
    sendWhisper,
    sendGMWhisper,
    callAgent,
    getWhisperHistory,
    getAgentCallHistory,
    getAgentCall,
    triggerNarrate,
    triggerSuggest,
    triggerConsistency,
    triggerReview,
    triggerFullReview,
    triggerNewScene,
    triggerGMEvaluate,
    triggerPlotCheck,
    triggerDetectOpportunities,
    triggerGenerateThreads,
    triggerSpawnMilestones,
    triggerSessionSummary,
    triggerPause,
    triggerResume,
    triggerCombatStart,
    triggerCombatEnd,
    rollDice,
    startSkillCheck,
    startAttack,
    startSpellCast,
    confirmPlayerRoll,
    declinePlayerRoll,
    getGameState,
    updateGameState,
    getPlotWeaverThreads,
    triggerPlotReview,
    getPlotReviewHistory,
    adjustThreadMomentum,
    detectOpportunities,
    getSwayStatus,
    sendSway,
    getActiveCombats,
    startCombat,
    endCombat,
    pauseCombat,
    resumeCombat,
    getCombat,
    getCombatLog,
    getPlayers,
    getNPCs,
    getPlotThreads,
    getCharacters,
    getCharacter,
    getLLMPresets,
    getLLMPreset,
    createLLMPreset,
    updateLLMPreset,
    deleteLLMPreset,
    testLLMPreset,
    setDefaultPreset,
    getLLMProviders,
    getPlotContext,
    findSimilarThreads,
    checkConsistency,
    getSessionSummary,
    getSystems,
    getLLMInteractions,
    getLLMInteraction,
    deleteLLMInteraction,
    getPresetUsage,
    getGameProviderUsage,
    cleanupOldLogs,
    getPendingToolCalls,
    confirmToolCall,
    getDiceHistory,
    getDiceRoll,
    getDiceStats,
    getPlayerDiceStats,
    getMessagesPaginated,
    searchMessages,
    getPromptTemplates,
    createPromptTemplate,
    updatePromptTemplate,
    deletePromptTemplate,
    getDefaultTemplate,
    getGameTemplates,
    createGameTemplate,
    updateGameTemplate,
    deleteGameTemplate,
    getGameTemplate,
    getProviderModels,
    getGameStatePage,
    updateGameStatePage,
    getGMTools,
    getGMToolsByCategory,
    executeGMTool,
    getCharacterSpells,
    updateCharacterSpells,
    updateSingleSpell,
    removeSpell,
    getSessionNotes,
    createSessionNote,
    updateSessionNote,
    deleteSessionNote,
  };
}
