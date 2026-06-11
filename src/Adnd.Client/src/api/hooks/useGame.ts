import { useState, useEffect, useCallback } from 'react';
import { api } from '../client';
import type { GameListItem, GameDetail, GMStatusResponse, SwayResponse } from '../../types';

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

  const createGame = async (name: string, systemId = 'dnd5e', systemVersion?: string, customSystemJson?: string, llmPresetId?: string, plotSeed?: string, gameParameters?: string, language = 'English') => {
    const game = await api.createGame(name, systemId, systemVersion, customSystemJson, llmPresetId, plotSeed, gameParameters, language);
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

  const joinByCode = async (code: string) => {
    const result = await api.joinByCode(code);
    await fetchGames();
    return result;
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
    joinByCode,
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

export function useGMStatus(gameId: string | undefined) {
  const [status, setStatus] = useState<GMStatusResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchStatus = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getGMStatus(gameId);
      setStatus(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchStatus();
  }, [fetchStatus]);

  const pause = async () => {
    await api.pauseGM(gameId!);
    await fetchStatus();
  };

  const resume = async () => {
    await api.resumeGM(gameId!);
    await fetchStatus();
  };

  return {
    status,
    isLoading,
    error,
    refetch: fetchStatus,
    pause,
    resume,
  };
}

export function useSway(gameId: string | undefined) {
  const [lastSway, setLastSway] = useState<SwayResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sway = useCallback(async (direction: string) => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.swayStory(gameId, direction);
      setLastSway(data);
      return data;
    } catch (e: any) {
      setError(e.message);
      return null;
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  return {
    sway,
    lastSway,
    isLoading,
    error,
  };
}

export function useSessions(gameId: string | undefined) {
  const [sessions, setSessions] = useState<{ id: string; title: string; description?: string; startedAt: string; endedAt?: string }[]>([]);
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
    await api.createSession(gameId!, title, description);
    await fetchSessions();
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
  const [players, setPlayers] = useState<{ id: string; characterName: string; role: string; status: string; joinedAt: string }[]>([]);
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
  const [npcs, setNpcs] = useState<{ id: string; name: string; description?: string }[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchNpcs = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getNPCs(gameId);
      setNpcs(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchNpcs();
  }, [fetchNpcs]);

  const createNPC = async (name: string, description?: string) => {
    await api.createNPC(gameId!, name, description);
    await fetchNpcs();
  };

  const updateNPC = async (npcId: string, updates: { name?: string; description?: string }) => {
    await api.updateNPC(npcId, updates);
    await fetchNpcs();
  };

  const deleteNPC = async (npcId: string) => {
    await api.deleteNPC(npcId);
    setNpcs(prev => prev.filter(n => n.id !== npcId));
  };

  return {
    npcs,
    isLoading,
    error,
    refetch: fetchNpcs,
    createNPC,
    updateNPC,
    deleteNPC,
  };
}

export function usePlotThreads(gameId: string | undefined) {
  const [threads, setThreads] = useState<{ id: string; title: string; description: string; status: string }[]>([]);
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
    await api.createPlotThread(gameId!, title, description);
    await fetchThreads();
  };

  const updateThread = async (threadId: string, updates: { title?: string; description?: string; status?: string }) => {
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
  const [characters, setCharacters] = useState<{ id: string; name: string; characterClass: string; level: number }[]>([]);
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

  const updateCharacter = async (characterId: string, updates: Record<string, any>) => {
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
  const [character, setCharacter] = useState<{ id: string; name: string; characterClass: string; level: number } | null>(null);
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

  const updateCharacter = async (updates: Record<string, any>) => {
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
  const [report, setReport] = useState<{ inconsistencies: string[] } | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const check = useCallback(async (messageCount = 50) => {
    if (!gameId) return;
    setIsLoading(true);
    try {
      const data = await api.checkConsistency(gameId, messageCount);
      setReport(data as any);
    } catch (e) {
      console.error('Consistency check failed', e);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  return { report, isLoading, check };
}
