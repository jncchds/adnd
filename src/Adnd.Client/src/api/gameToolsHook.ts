import { useState, useCallback } from 'react';
import { api } from './client';
import type { SessionNote, DiceStatsResponse, PlayerDiceStatsResponse, MessagePaginationResponse, MessageSearchResponse, PromptTemplate, MessagePaginated } from '../types';

// ==================== Unified Message Type ====================
// All game events flow through the unified chat

export type UnifiedMessageType =
  // Chat messages
  | 'inGamePublic'
  | 'inGameWhisper'
  | 'oocPublic'
  | 'oocWhisper'
  // Game actions
  | 'dice'
  | 'skillCheck'
  | 'attack'
  | 'spellCast'
  // Combat
  | 'combatStart'
  | 'combatEnd'
  | 'combatPause'
  | 'combatResume'
  | 'initiative'
  | 'initiativeComplete'
  | 'turnAdvanced'
  | 'turnRetreated'
  | 'turnSet'
  | 'damage'
  | 'heal'
  | 'deathSave'
  | 'conditionApplied'
  | 'conditionRemoved'
  | 'xpGranted'
  | 'levelUp'
  | 'sanLoss'
  | 'sanRecovery'
  | 'sanCheck'
  // Action economy
  | 'actionSpent'
  | 'bonusActionSpent'
  | 'reactionSpent'
  | 'movementSpent'
  | 'actionsRefreshed'
  // Combat state
  | 'participantAdded'
  | 'participantRemoved'
  | 'gridSet'
  | 'positionSet'
  | 'combatMove'
  | 'itemAdded'
  | 'itemRemoved'
  | 'itemEquipped'
  | 'itemUnequipped'
  // Player lifecycle
  | 'playerJoined'
  | 'playerLeft'
  | 'playerDisconnected'
  | 'playerReconnected'
  | 'playerRoleChanged'
  // Character lifecycle
  | 'characterCreated'
  | 'characterUpdated'
  // Session/Game lifecycle
  | 'sessionCreated'
  | 'sessionClosed'
  | 'gameStarted'
  | 'gamePaused'
  | 'gameResumed'
  | 'gameArchived'
  // GM / AI
  | 'gm'
  | 'narration'
  | 'suggestion'
  | 'consistencyCheck'
  | 'plotReview'
  | 'plotThreadCreated'
  | 'plotThreadUpdated'
  | 'npcEvent'
  // System / meta
  | 'system'
  | 'agentCall'
  | 'agentResponse'
  | 'toolCall'
  | 'toolCallConfirmed'
  | 'toolCallDenied'
  | 'playerRollRequest'
  | 'playerRollConfirmed'
  | 'playerRollDeclined'
  | 'playerRollResult'
  | 'stateChange'
  | 'aiCombatSuggestion'
  | 'aiCombatAutoResolve';

export interface UnifiedMessage {
  id: string | number;
  type: UnifiedMessageType;
  content: string;
  senderName: string;
  senderRole: string;
  timestamp: string;
  isSystem?: boolean;
  isWhisper?: boolean;
  whisperTo?: string;
  // Dice
  diceFormula?: string;
  diceTotal?: number;
  diceRolls?: number[];
  // Skill check
  skill?: string;
  skillDC?: number;
  skillResult?: string;
  // Attack
  attackWeapon?: string;
  attackTarget?: string;
  attackHit?: boolean;
  attackDamage?: number;
  // Combat
  combatName?: string;
  participantName?: string;
  participantType?: string;
  hp?: number;
  maxHP?: number;
  ac?: number;
  initiative?: number;
  damage?: number;
  healAmount?: number;
  conditionName?: string;
  conditionDuration?: number;
  xpAmount?: number;
  newLevel?: number;
  sanAmount?: number;
  // Action economy
  actionsRemaining?: number;
  bonusActionsRemaining?: number;
  reactionsRemaining?: number;
  movementsRemaining?: number;
  // Agent
  agentFrom?: string;
  agentAction?: string;
  agentStatus?: string;
  // Tool call
  toolName?: string;
  // Grid
  gridWidth?: number;
  gridHeight?: number;
  gridX?: number;
  gridY?: number;
  // Item
  itemName?: string;
  itemType?: string;
  // Metadata
  metadata?: Record<string, any>;
  extra?: React.ReactNode;
}

// ==================== Session Notes Hook ====================

export function useSessionNotes(gameId: string | undefined, sessionId: string | undefined) {
  const [notes, setNotes] = useState<SessionNote[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchNotes = useCallback(async () => {
    if (!gameId || !sessionId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getSessionNotes(gameId, sessionId);
      setNotes(data.notes);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId]);

  const createNote = async (title: string, content: string) => {
    if (!gameId || !sessionId) return null;
    const note = await api.createSessionNote(gameId, sessionId, title, content);
    setNotes(prev => [note, ...prev]);
    return note;
  };

  const updateNote = async (noteId: string, title: string, content: string) => {
    if (!gameId) return null;
    const note = await api.updateSessionNote(gameId, noteId, title, content);
    setNotes(prev => prev.map(n => n.id === noteId ? note : n));
    return note;
  };

  const deleteNote = async (noteId: string) => {
    if (!gameId) return;
    await api.deleteSessionNote(gameId, noteId);
    setNotes(prev => prev.filter(n => n.id !== noteId));
  };

  return {
    notes,
    isLoading,
    error,
    refetch: fetchNotes,
    createNote,
    updateNote,
    deleteNote,
  };
}

// ==================== Dice Statistics Hook ====================

export function useDiceStats(gameId: string | undefined, sessionId?: string | undefined) {
  const [stats, setStats] = useState<DiceStatsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchStats = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getDiceStats(gameId, sessionId);
      setStats(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId]);

  return {
    stats,
    isLoading,
    error,
    refetch: fetchStats,
  };
}

export function usePlayerDiceStats(gameId: string | undefined, playerId: string | undefined, sessionId?: string | undefined) {
  const [stats, setStats] = useState<PlayerDiceStatsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchStats = useCallback(async () => {
    if (!gameId || !playerId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getPlayerDiceStats(gameId, playerId, sessionId);
      setStats(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, playerId, sessionId]);

  return {
    stats,
    isLoading,
    error,
    refetch: fetchStats,
  };
}

// ==================== Messages Infinite Scroll Hook ====================
// Loads newest messages first, then older ones when scrolling up.

export function useMessagesPaginated(gameId: string | undefined, sessionId: string | undefined) {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(50);
  const [messages, setMessages] = useState<MessagePaginationResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchMessages = useCallback(async (pageNum: number = 1, type?: number) => {
    if (!gameId || !sessionId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getMessagesPaginated(gameId, sessionId, pageNum, pageSize, type);
      setMessages(data);
      setPage(pageNum);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId, pageSize]);

  const loadMore = useCallback(() => {
    if (messages && page < messages.totalPages) {
      fetchMessages(page + 1);
    }
  }, [messages, page, fetchMessages]);

  return {
    messages,
    page,
    pageSize,
    isLoading,
    error,
    refetch: fetchMessages,
    loadMore,
    goToPage: fetchMessages,
  };
}

// ==================== Messages Infinite Scroll Hook (newest first) ====================
// Loads newest messages first, then older ones when scrolling up.
// Returns UnifiedMessage[] for direct use in the chat panel.

export function useMessagesInfiniteScroll(gameId: string | undefined, sessionId: string | undefined) {
  const [pageSize] = useState(50);
  const [messages, setMessages] = useState<UnifiedMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasMore, setHasMore] = useState(true);
  const [oldestAnchorId, setOldestAnchorId] = useState<string | null>(null);
  const totalMessages = messages.length;

  // Convert MessagePaginated to UnifiedMessage — handles ALL message types
  const toUnified = (msg: MessagePaginated): UnifiedMessage => {
    // Parse metadata if present
    let metadata: Record<string, any> | undefined;
    try {
      if (msg.metadata && typeof msg.metadata === 'object') {
        metadata = msg.metadata as Record<string, any>;
      } else if (msg.metadata && typeof msg.metadata === 'string') {
        metadata = JSON.parse(msg.metadata);
      }
    } catch {
      // ignore parse errors
    }

    // Map backend MessageType to frontend UnifiedMessageType
    let type: UnifiedMessageType;
    const backendType = msg.type;

    if (msg.isOOC) {
      if (backendType === 3) {
        type = 'oocWhisper';
      } else {
        type = 'oocPublic';
      }
    } else if (backendType === 1) {
      type = 'inGameWhisper';
    } else {
      // Map all other message types
      switch (backendType) {
        case 0: type = 'inGamePublic'; break; // InGamePublic
        case 2: type = 'oocPublic'; break;   // OOCPublic
        case 4: type = 'system'; break;       // Action (legacy)
        case 5: type = 'dice'; break;         // Dice
        case 6: type = 'system'; break;       // System
        case 7: type = 'gm'; break;           // GM
        case 8: type = 'agentCall'; break;    // AgentCall
        case 9: type = 'agentResponse'; break; // AgentResponse
        case 10: type = 'skillCheck'; break;  // SkillCheck
        case 11: type = 'attack'; break;      // Attack
        case 12: type = 'spellCast'; break;   // SpellCast
        case 20: type = 'combatStart'; break; // CombatStart
        case 21: type = 'combatEnd'; break;   // CombatEnd
        case 22: type = 'combatPause'; break; // CombatPause
        case 23: type = 'combatResume'; break; // CombatResume
        case 24: type = 'initiative'; break;  // Initiative
        case 25: type = 'initiativeComplete'; break; // InitiativeComplete
        case 26: type = 'turnAdvanced'; break; // TurnAdvanced
        case 27: type = 'turnRetreated'; break; // TurnRetreated
        case 28: type = 'turnSet'; break;     // TurnSet
        case 29: type = 'damage'; break;      // DamageDealt
        case 30: type = 'damage'; break;      // DamageTaken
        case 31: type = 'heal'; break;        // Healed
        case 32: type = 'deathSave'; break;   // DeathSave
        case 33: type = 'conditionApplied'; break; // ConditionApplied
        case 34: type = 'conditionRemoved'; break; // ConditionRemoved
        case 35: type = 'xpGranted'; break;   // XPGranted
        case 36: type = 'levelUp'; break;     // LevelUp
        case 37: type = 'sanLoss'; break;     // SANLoss
        case 38: type = 'sanRecovery'; break; // SANRecovery
        case 39: type = 'sanCheck'; break;    // SANCheck
        case 40: type = 'actionSpent'; break; // ActionSpent
        case 41: type = 'bonusActionSpent'; break; // BonusActionSpent
        case 42: type = 'reactionSpent'; break; // ReactionSpent
        case 43: type = 'movementSpent'; break; // MovementSpent
        case 44: type = 'actionsRefreshed'; break; // ActionsRefreshed
        case 50: type = 'participantAdded'; break; // ParticipantAdded
        case 51: type = 'participantRemoved'; break; // ParticipantRemoved
        case 52: type = 'gridSet'; break;     // GridSet
        case 53: type = 'positionSet'; break; // PositionSet
        case 54: type = 'combatMove'; break;  // CombatMove
        case 55: type = 'itemAdded'; break;   // ItemAdded
        case 56: type = 'itemRemoved'; break; // ItemRemoved
        case 57: type = 'itemEquipped'; break; // ItemEquipped
        case 58: type = 'itemUnequipped'; break; // ItemUnequipped
        case 60: type = 'playerJoined'; break; // PlayerJoined
        case 61: type = 'playerLeft'; break;  // PlayerLeft
        case 62: type = 'playerDisconnected'; break; // PlayerDisconnected
        case 63: type = 'playerReconnected'; break; // PlayerReconnected
        case 64: type = 'playerRoleChanged'; break; // PlayerRoleChanged
        case 70: type = 'characterCreated'; break; // CharacterCreated
        case 71: type = 'characterUpdated'; break; // CharacterUpdated
        case 80: type = 'sessionCreated'; break; // SessionCreated
        case 81: type = 'sessionClosed'; break; // SessionClosed
        case 82: type = 'gameStarted'; break; // GameStarted
        case 83: type = 'gamePaused'; break;  // GamePaused
        case 84: type = 'gameResumed'; break; // GameResumed
        case 85: type = 'gameArchived'; break; // GameArchived
        case 86: type = 'narration'; break;   // Narration
        case 87: type = 'suggestion'; break;  // Suggestion
        case 88: type = 'consistencyCheck'; break; // ConsistencyCheck
        case 89: type = 'plotReview'; break;  // PlotReview
        case 90: type = 'plotThreadCreated'; break; // PlotThreadCreated
        case 91: type = 'plotThreadUpdated'; break; // PlotThreadUpdated
        case 92: type = 'npcEvent'; break;    // NPCEvent
        case 93: type = 'toolCall'; break;    // ToolCall
        case 94: type = 'toolCallConfirmed'; break; // ToolCallConfirmed
        case 95: type = 'toolCallDenied'; break; // ToolCallDenied
        case 96: type = 'playerRollRequest'; break; // PlayerRollRequest
        case 97: type = 'playerRollConfirmed'; break; // PlayerRollConfirmed
        case 98: type = 'playerRollDeclined'; break; // PlayerRollDeclined
        case 99: type = 'playerRollResult'; break; // PlayerRollResult
        case 100: type = 'stateChange'; break; // StateChange
        case 101: type = 'aiCombatSuggestion'; break; // AICombatSuggestion
        case 102: type = 'aiCombatAutoResolve'; break; // AICombatAutoResolve
        default: type = 'system'; break;
      }
    }

    return {
      id: msg.id,
      type,
      content: msg.content,
      senderName: msg.playerName,
      senderRole: msg.playerId ? 'Player' : 'System',
      timestamp: msg.createdAt,
      isSystem: type.startsWith('combat') || type.startsWith('damage') || type.startsWith('heal') ||
        type.startsWith('condition') || type.startsWith('initiative') || type.startsWith('turn') ||
        type.startsWith('action') || type.startsWith('participant') || type.startsWith('grid') ||
        type.startsWith('position') || type.startsWith('combatMove') || type.startsWith('item') ||
        type.startsWith('player') || type.startsWith('character') || type.startsWith('session') ||
        type.startsWith('game') || type.startsWith('xp') || type.startsWith('level') ||
        type.startsWith('san') || type.startsWith('death') || type.startsWith('system') ||
        type.startsWith('agent') || type.startsWith('tool') || type.startsWith('playerRoll') ||
        type.startsWith('stateChange') || type.startsWith('aiCombat') ||
        type === 'dice' || type === 'skillCheck' || type === 'attack' || type === 'spellCast',
      isWhisper: type === 'inGameWhisper' || type === 'oocWhisper',
      metadata,
    };
  };

  // Load the newest batch on mount
  const loadInitial = useCallback(async () => {
    if (!gameId || !sessionId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getMessagesPaginated(gameId, sessionId, 1, pageSize);
      const unified = data.messages.map(toUnified);
      setMessages(unified);
      setHasMore(data.hasMore);
      if (data.messages.length > 0) {
        // Messages are newest-first; the last item is the oldest in this batch
        setOldestAnchorId(data.messages[data.messages.length - 1].id);
      }
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId, pageSize]);

  // Load older messages when scrolling up
  const loadMoreOldest = useCallback(async () => {
    if (!gameId || !sessionId || !hasMore || !oldestAnchorId || isLoadingMore) return;
    setIsLoadingMore(true);
    try {
      const data = await api.getMessagesPaginated(gameId, sessionId, 1, pageSize, undefined, oldestAnchorId);
      if (data.messages.length > 0) {
        // Prepend older messages
        const unified = data.messages.map(toUnified);
        setMessages(prev => [...unified, ...prev]);
        setHasMore(data.hasMore);
        setOldestAnchorId(data.messages[data.messages.length - 1].id);
      }
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoadingMore(false);
    }
  }, [gameId, sessionId, hasMore, oldestAnchorId, isLoadingMore]);

  // Add a single live message (from SignalR) to the newest end
  const addMessage = useCallback((msg: UnifiedMessage) => {
    setMessages(prev => [...prev, msg]);
  }, []);

  // Remove a message (e.g. if deleted)
  const removeMessage = useCallback((msgId: string) => {
    setMessages(prev => prev.filter(m => m.id !== msgId));
  }, []);

  // Update a message by id (e.g. agent call status change)
  const updateMessage = useCallback((msgId: string | number, updater: (msg: UnifiedMessage) => UnifiedMessage) => {
    setMessages(prev => prev.map(m => m.id === msgId ? updater(m) : m));
  }, []);

  return {
    messages,
    totalMessages,
    isLoading,
    isLoadingMore,
    hasMore,
    error,
    loadInitial,
    loadMoreOldest,
    addMessage,
    updateMessage,
    removeMessage,
  };
}

// ==================== Message Search Hook ====================

export function useMessageSearch(gameId: string | undefined, sessionId: string | undefined) {
  const [results, setResults] = useState<MessageSearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const search = useCallback(async (query: string, queryEmbedding: number[], limit = 10) => {
    if (!gameId || !sessionId) return null;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.searchMessages(gameId, sessionId, query, queryEmbedding, limit);
      setResults(data);
      return data;
    } catch (e: any) {
      setError(e.message);
      return null;
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId]);

  return {
    results,
    isLoading,
    error,
    search,
  };
}

// ==================== Prompt Templates Hook ====================

export function usePromptTemplates(gameId: string | undefined) {
  const [templates, setTemplates] = useState<PromptTemplate[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchTemplates = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getPromptTemplates(gameId);
      setTemplates(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  const createTemplate = async (name: string, type: string, prompt: string) => {
    if (!gameId) return null;
    const template = await api.createPromptTemplate(gameId, name, type, prompt);
    setTemplates(prev => [template, ...prev]);
    return template;
  };

  const updateTemplate = async (templateId: string, name: string, type: string, prompt: string, isActive?: boolean, isDefault?: boolean) => {
    if (!gameId) return null;
    const template = await api.updatePromptTemplate(gameId, templateId, name, type, prompt, isActive, isDefault);
    setTemplates(prev => prev.map(t => t.id === templateId ? template : t));
    return template;
  };

  const deleteTemplate = async (templateId: string) => {
    if (!gameId) return;
    await api.deletePromptTemplate(gameId, templateId);
    setTemplates(prev => prev.filter(t => t.id !== templateId));
  };

  const getDefaultTemplate = async (type: string) => {
    if (!gameId) return null;
    return api.getDefaultTemplate(gameId, type);
  };

  return {
    templates,
    isLoading,
    error,
    refetch: fetchTemplates,
    createTemplate,
    updateTemplate,
    deleteTemplate,
    getDefaultTemplate,
  };
}
