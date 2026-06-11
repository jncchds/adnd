import { useState, useEffect, useCallback } from 'react';
import { gamesGetSessions, gamesCreateSession, gamesCloseSession, gamesGetPlayers } from '../../api/games/gameApi';

export function useSessions(gameId: string | undefined) {
  const [sessions, setSessions] = useState<{ id: string; title: string; description?: string; startedAt: string; endedAt?: string }[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchSessions = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await gamesGetSessions(gameId);
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
    await gamesCreateSession(gameId!, title, description);
    await fetchSessions();
  };

  const closeSession = async (sessionId: string) => {
    await gamesCloseSession(gameId!, sessionId);
    await fetchSessions();
  };

  return { sessions, isLoading, error, refetch: fetchSessions, createSession, closeSession };
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
      const data = await gamesGetPlayers(gameId);
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

  return { players, isLoading, error, refetch: fetchPlayers };
}
