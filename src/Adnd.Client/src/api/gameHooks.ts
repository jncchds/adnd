import { useState, useEffect, useCallback } from 'react';
import { api, GameListItem, GameDetail, GameSessionListItem, GameSessionDetail, PlayerListItem, NPCListItem, PlotThreadListItem, CharacterListItem, CharacterDetail, ConsistencyReport, LLMPreset, LLMPresetDetail, CreateLLMPresetRequest, UpdateLLMPresetRequest, LLMInteractionLog, PresetUsageSummary } from './client';
import { useEntity } from './useEntity';

export function useGames() {
  const [games, setGames] = useState<GameListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchGames = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getGames();
      setGames(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchGames();
  }, [fetchGames]);

  const createGame = async (name: string, systemId = 'dnd5e') => {
    const game = await api.createGame(name, systemId);
    setGames(prev => [...prev, game]);
    return game;
  };

  const deleteGame = async (id: string) => {
    await api.deleteGame(id);
    setGames(prev => prev.filter(g => g.id !== id));
  };

  const joinGame = async (id: string) => {
    await api.joinGame(id);
    await fetchGames();
  };

  const leaveGame = async (id: string) => {
    await api.leaveGame(id);
    await fetchGames();
  };

  const generateInvite = async (id: string) => {
    return api.generateInvite(id);
  };

  const startGame = async (id: string) => {
    await api.startGame(id);
    await fetchGames();
  };

  const archiveGame = async (id: string) => {
    await api.archiveGame(id);
    await fetchGames();
  };

  return {
    games,
    isLoading,
    error,
    refetch: fetchGames,
    createGame,
    deleteGame,
    joinGame,
    leaveGame,
    generateInvite,
    startGame,
    archiveGame,
  };
}

export function useGame(id: string | undefined) {
  const [game, setGame] = useState<GameDetail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchGame = useCallback(async () => {
    if (!id) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getGame(id);
      setGame(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchGame();
  }, [fetchGame]);

  return { game, isLoading, error, refetch: fetchGame };
}

export function useSessions(gameId: string | undefined) {
  const result = useEntity<GameSessionListItem, GameSessionDetail, GameSessionDetail, void>(
    () => gameId ? api.getSessions(gameId) : Promise.resolve([]),
    [gameId],
    (data) => api.createSession(gameId!, (data as any).title ?? '', (data as any).description) as Promise<GameSessionDetail>,
    undefined,
    undefined
  );

  const closeSession = async (sessionId: string) => {
    await api.closeSession(gameId!, sessionId);
    await result.refetch();
  };

  return {
    sessions: result.items,
    isLoading: result.isLoading,
    error: result.error,
    refetch: result.refetch,
    createSession: result.create,
    closeSession,
  };
}

export function usePlayers(gameId: string | undefined) {
  const [players, setPlayers] = useState<PlayerListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchPlayers = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getPlayers(gameId);
      setPlayers(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchPlayers();
  }, [fetchPlayers]);

  return {
    players,
    isLoading,
    error,
    refetch: fetchPlayers,
  };
}

export function useNPCs(gameId: string | undefined) {
  const result = useEntity<NPCListItem, NPCListItem, NPCListItem, void>(
    () => gameId ? api.getNPCs(gameId) : Promise.resolve([]),
    [gameId],
    (data) => api.createNPC(gameId!, (data as any).name ?? '', (data as any).description) as Promise<NPCListItem>,
    (id) => api.deleteNPC(id) as Promise<void>,
    (id, data) => api.updateNPC(id, data) as Promise<NPCListItem>
  );

  return {
    npcs: result.items,
    isLoading: result.isLoading,
    error: result.error,
    refetch: result.refetch,
    createNPC: result.create,
    updateNPC: result.update,
    deleteNPC: result.remove,
  };
}

export function usePlotThreads(gameId: string | undefined) {
  const result = useEntity<PlotThreadListItem, PlotThreadListItem, PlotThreadListItem, void>(
    () => gameId ? api.getPlotThreads(gameId) : Promise.resolve([]),
    [gameId],
    (data) => api.createPlotThread(gameId!, (data as any).title ?? '', (data as any).description ?? '') as Promise<PlotThreadListItem>,
    undefined,
    (id, data) => api.updatePlotThread(id, data) as Promise<PlotThreadListItem>
  );

  return {
    threads: result.items,
    isLoading: result.isLoading,
    error: result.error,
    refetch: result.refetch,
    createThread: result.create,
    updateThread: result.update,
  };
}

export function useCharacters(gameId: string | undefined) {
  const result = useEntity<CharacterListItem, unknown, unknown, unknown>(
    () => gameId ? api.getCharacters(gameId) : Promise.resolve([]),
    [gameId],
    undefined,
    undefined,
    (id, data) => api.updateCharacter(id, data) as Promise<CharacterListItem>
  );

  return {
    characters: result.items,
    isLoading: result.isLoading,
    error: result.error,
    refetch: result.refetch,
    updateCharacter: result.update,
  };
}

export function useCharacter(characterId: string | undefined) {
  const [character, setCharacter] = useState<CharacterDetail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCharacter = useCallback(async () => {
    if (!characterId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getCharacter(characterId);
      setCharacter(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [characterId]);

  useEffect(() => {
    fetchCharacter();
  }, [fetchCharacter]);

  const updateCharacter = async (updates: any) => {
    await api.updateCharacter(characterId!, updates);
    await fetchCharacter();
  };

  return {
    character,
    isLoading,
    error,
    refetch: fetchCharacter,
    updateCharacter,
  };
}

export function useConsistency(gameId: string | undefined) {
  const [report, setReport] = useState<ConsistencyReport | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const check = useCallback(async (messageCount = 50) => {
    if (!gameId) return;
    setIsLoading(true);
    try {
      const data = await api.checkConsistency(gameId, messageCount);
      setReport(data as ConsistencyReport);
    } catch (e) {
      console.error('Consistency check failed', e);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  return { report, isLoading, check };
}

// ==================== LLM Preset Hooks ====================

export function useLLMPresets() {
  const result = useEntity<LLMPreset, LLMPresetDetail, LLMPresetDetail, void>(
    () => api.getLLMPresets(),
    [],
    (data) => api.createLLMPreset(data as CreateLLMPresetRequest) as Promise<LLMPresetDetail>,
    (id) => api.deleteLLMPreset(id) as Promise<void>,
    (id, data) => api.updateLLMPreset(id, data as UpdateLLMPresetRequest) as Promise<LLMPresetDetail>
  );

  const testConnection = async (presetId: string) => {
    return api.testLLMPreset(presetId);
  };

  const setDefault = async (presetId: string) => {
    await api.setDefaultPreset(presetId);
    await result.refetch();
  };

  return {
    presets: result.items,
    isLoading: result.isLoading,
    error: result.error,
    refetch: result.refetch,
    createPreset: result.create,
    updatePreset: result.update,
    deletePreset: result.remove,
    testConnection,
    setDefault,
  };
}

// ==================== LLM Interaction Log Hooks ====================

export function useLLMInteractions(gameId?: string) {
  const [logs, setLogs] = useState<LLMInteractionLog[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchLogs = useCallback(async (params?: {
    presetId?: string;
    providerType?: string;
    from?: string;
    to?: string;
    limit?: number;
  }) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getLLMInteractions(params);
      setLogs(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const fetchGameLogs = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getLLMInteractions({ gameId, limit: 200 });
      setLogs(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  const deleteLog = async (logId: string) => {
    await api.deleteLLMInteraction(logId);
    setLogs(prev => prev.filter(l => l.id !== logId));
  };

  const getUsage = useCallback(async (from?: string, to?: string): Promise<PresetUsageSummary[]> => {
    return api.getPresetUsage(from, to);
  }, []);

  return {
    logs,
    isLoading,
    error,
    refetch: gameId ? fetchGameLogs : fetchLogs,
    fetchLogs,
    deleteLog,
    getUsage,
  };
}
