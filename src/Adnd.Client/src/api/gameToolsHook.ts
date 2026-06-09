import { useState, useCallback } from 'react';
import { api, SessionNote, DiceStatsResponse, PlayerDiceStatsResponse, MessagePaginationResponse, MessageSearchResponse, PromptTemplate } from './client';

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

// ==================== Messages Paginated Hook ====================

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
