import { useState, useEffect, useCallback } from 'react';
import { api, GameListItem, GameDetail, GameSessionListItem, PlayerListItem, NPCListItem, PlotThreadListItem, CharacterListItem, CharacterDetail, ConsistencyReport, LLMPreset, CreateLLMPresetRequest, UpdateLLMPresetRequest, LLMInteractionLog, PresetUsageSummary } from './client';

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
  const [sessions, setSessions] = useState<GameSessionListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchSessions = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getSessions(gameId);
      setSessions(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchSessions();
  }, [fetchSessions]);

  const createSession = async (title: string, description?: string) => {
    const session = await api.createSession(gameId!, title, description);
    setSessions(prev => [{ ...session, messageCount: 0 }, ...prev]);
    return session;
  };

  const closeSession = async (sessionId: string) => {
    await api.closeSession(gameId!, sessionId);
    await fetchSessions();
  };

  return {
    sessions,
    isLoading,
    error,
    refetch: fetchSessions,
    createSession,
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
  const [npcs, setNPCs] = useState<NPCListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchNPCs = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getNPCs(gameId);
      setNPCs(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchNPCs();
  }, [fetchNPCs]);

  const createNPC = async (name: string, description?: string) => {
    const npc = await api.createNPC(gameId!, name, description);
    setNPCs(prev => [...prev, npc as NPCListItem]);
    return npc;
  };

  const updateNPC = async (npcId: string, updates: any) => {
    await api.updateNPC(npcId, updates);
    await fetchNPCs();
  };

  const deleteNPC = async (npcId: string) => {
    await api.deleteNPC(npcId);
    setNPCs(prev => prev.filter(n => n.id !== npcId));
  };

  return {
    npcs,
    isLoading,
    error,
    refetch: fetchNPCs,
    createNPC,
    updateNPC,
    deleteNPC,
  };
}

export function usePlotThreads(gameId: string | undefined) {
  const [threads, setThreads] = useState<PlotThreadListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchThreads = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getPlotThreads(gameId);
      setThreads(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchThreads();
  }, [fetchThreads]);

  const createThread = async (title: string, description: string) => {
    const thread = await api.createPlotThread(gameId!, title, description);
    setThreads(prev => [...prev, thread as PlotThreadListItem]);
    return thread;
  };

  const updateThread = async (threadId: string, updates: any) => {
    await api.updatePlotThread(threadId, updates);
    await fetchThreads();
  };

  return {
    threads,
    isLoading,
    error,
    refetch: fetchThreads,
    createThread,
    updateThread,
  };
}

export function useCharacters(gameId: string | undefined) {
  const [characters, setCharacters] = useState<CharacterListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCharacters = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getCharacters(gameId);
      setCharacters(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchCharacters();
  }, [fetchCharacters]);

  const updateCharacter = async (characterId: string, updates: any) => {
    await api.updateCharacter(characterId, updates);
    await fetchCharacters();
  };

  return {
    characters,
    isLoading,
    error,
    refetch: fetchCharacters,
    updateCharacter,
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
  const [presets, setPresets] = useState<LLMPreset[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchPresets = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getLLMPresets();
      setPresets(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchPresets();
  }, [fetchPresets]);

  const createPreset = async (request: CreateLLMPresetRequest) => {
    const preset = await api.createLLMPreset(request);
    setPresets(prev => [...prev, preset]);
    return preset;
  };

  const updatePreset = async (presetId: string, request: UpdateLLMPresetRequest) => {
    const preset = await api.updateLLMPreset(presetId, request);
    setPresets(prev => prev.map(p => p.id === presetId ? preset : p));
    return preset;
  };

  const deletePreset = async (presetId: string) => {
    await api.deleteLLMPreset(presetId);
    setPresets(prev => prev.filter(p => p.id !== presetId));
  };

  const testConnection = async (presetId: string) => {
    return api.testLLMPreset(presetId);
  };

  const setDefault = async (presetId: string) => {
    await api.setDefaultPreset(presetId);
    await fetchPresets();
  };

  return {
    presets,
    isLoading,
    error,
    refetch: fetchPresets,
    createPreset,
    updatePreset,
    deletePreset,
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
