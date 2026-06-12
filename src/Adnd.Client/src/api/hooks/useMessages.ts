import { useState, useCallback, useEffect } from 'react';
import { messagesGetPaginated, messagesSearch } from '../../api/messages/messageApi';
import { gmGetSessionNotes, gmCreateSessionNote, gmUpdateSessionNote, gmDeleteSessionNote, gmGetDiceStats, gmGetPlayerDiceStats } from '../../api/gm/gmApi';
import type { SessionNote, DiceStatsResponse, PlayerDiceStatsResponse, MessageSearchResponse, MessagePaginated } from '../../types';

// ==================== Unified Message Types ====================

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
  // Action economy
  actionsRemaining?: number;
  bonusActionsRemaining?: number;
  reactionsRemaining?: number;
  movementsRemaining?: number;
  // Agent
  agentStatus?: string;
  // Character stats (for combat events)
  hp?: number;
  maxHP?: number;
  ac?: number;
  // Condition
  conditionDuration?: number;
}

export function useSessionNotes(gameId: string | undefined, sessionId: string | undefined) {
  const [notes, setNotes] = useState<SessionNote[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchNotes = useCallback(async () => {
    if (!gameId || !sessionId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await gmGetSessionNotes(gameId, sessionId);
      setNotes(data.notes);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId]);

  useEffect(() => {
    fetchNotes();
  }, [fetchNotes]);

  const createNote = async (title: string, content: string) => {
    await gmCreateSessionNote(gameId!, sessionId!, title, content);
    await fetchNotes();
  };

  const updateNote = async (noteId: string, title: string, content: string) => {
    await gmUpdateSessionNote(gameId!, noteId, title, content);
    await fetchNotes();
  };

  const deleteNote = async (noteId: string) => {
    await gmDeleteSessionNote(gameId!, noteId);
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

export function useDiceStats(gameId: string | undefined, sessionId?: string) {
  const [stats, setStats] = useState<DiceStatsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchStats = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await gmGetDiceStats(gameId, sessionId);
      setStats(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId]);

  useEffect(() => {
    fetchStats();
  }, [fetchStats]);

  return {
    stats,
    isLoading,
    error,
    refetch: fetchStats,
  };
}

export function usePlayerDiceStats(gameId: string | undefined, playerId: string | undefined, sessionId?: string) {
  const [stats, setStats] = useState<PlayerDiceStatsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchStats = useCallback(async () => {
    if (!gameId || !playerId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await gmGetPlayerDiceStats(gameId, playerId, sessionId);
      setStats(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, playerId, sessionId]);

  useEffect(() => {
    fetchStats();
  }, [fetchStats]);

  return {
    stats,
    isLoading,
    error,
    refetch: fetchStats,
  };
}

export function useMessagesPaginated(gameId: string | undefined, sessionId: string | undefined) {
  const [messages, setMessages] = useState<MessagePaginated[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchMessages = useCallback(async (pageNum: number, pageSize = 50, type?: number, anchorId?: string) => {
    if (!gameId || !sessionId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await messagesGetPaginated(gameId, sessionId, pageNum, pageSize, type, anchorId);
      setMessages(data.messages);
      setTotalPages(data.totalPages);
      setPage(pageNum);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId]);

  return {
    messages,
    page,
    totalPages,
    isLoading,
    error,
    refetch: fetchMessages,
    nextPage: () => fetchMessages(page + 1),
    prevPage: () => fetchMessages(Math.max(1, page - 1)),
  };
}

export function useMessagesInfiniteScroll(gameId: string | undefined, sessionId: string | undefined) {
  const [messages, setMessages] = useState<MessagePaginated[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [anchorId, setAnchorId] = useState<string | undefined>(undefined);

  const loadMore = useCallback(async () => {
    if (!gameId || !sessionId || !hasMore) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await messagesGetPaginated(gameId, sessionId, 1, 50, undefined, anchorId);
      if (data.messages.length > 0) {
        setMessages(prev => [...prev, ...data.messages]);
        setAnchorId(data.messages[data.messages.length - 1].id);
        setHasMore(data.hasMore);
      } else {
        setHasMore(false);
      }
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId, hasMore, anchorId]);

  const loadOldest = useCallback(async () => {
    if (!gameId || !sessionId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await messagesGetPaginated(gameId, sessionId, 1, 50);
      if (data.messages.length > 0) {
        setMessages(prev => [...data.messages, ...prev]);
        setAnchorId(data.messages[0].id);
        setHasMore(data.hasMore);
      } else {
        setHasMore(false);
      }
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId, sessionId]);

  return {
    messages,
    isLoading,
    hasMore,
    error,
    loadMore,
    loadOldest,
  };
}

export function useMessageSearch(gameId: string | undefined, sessionId: string | undefined) {
  const [results, setResults] = useState<MessageSearchResponse['results']>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const search = useCallback(async (query: string, queryEmbedding: number[], limit = 10) => {
    if (!gameId || !sessionId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await messagesSearch(gameId, sessionId, query, queryEmbedding, limit);
      setResults(data.results);
    } catch (e: any) {
      setError(e.message);
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
