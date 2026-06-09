import { useState, useCallback } from 'react';
import { api, SessionNote, DiceStatsResponse, PlayerDiceStatsResponse, MessagePaginationResponse, MessageSearchResponse, PromptTemplate, MessagePaginated } from './client';

// ==================== Unified Message Type ====================

export type UnifiedMessageType =
  | 'inGamePublic'
  | 'inGameWhisper'
  | 'oocPublic'
  | 'oocWhisper'
  | 'dice'
  | 'skillCheck'
  | 'attack'
  | 'system'
  | 'agentCall'
  | 'agentResponse';

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
  diceFormula?: string;
  diceTotal?: number;
  diceRolls?: number[];
  skill?: string;
  skillDC?: number;
  skillResult?: string;
  attackWeapon?: string;
  attackTarget?: string;
  attackHit?: boolean;
  attackDamage?: number;
  agentFrom?: string;
  agentAction?: string;
  agentStatus?: string;
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

  // Convert MessagePaginated to UnifiedMessage
  const toUnified = (msg: MessagePaginated): UnifiedMessage => ({
    id: msg.id,
    type: msg.isOOC ? 'oocPublic' : 'inGamePublic',
    content: msg.content,
    senderName: msg.playerName,
    senderRole: msg.playerId ? 'Player' : 'System',
    timestamp: msg.createdAt,
  });

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
