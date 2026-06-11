import { useState, useEffect, useCallback } from 'react';
import { gamesGetGame, gamesGetGMStatus, gamesPauseGM, gamesResumeGM, gamesSwayStory } from '../../api/games/gameApi';
import type { GameDetail, GMStatusResponse, SwayResponse } from '../../types';

export function useGame(id: string | undefined) {
  const [game, setGame] = useState<GameDetail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchGame = useCallback(async () => {
    if (!id) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await gamesGetGame(id);
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
      const data = await gamesGetGMStatus(gameId);
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
    await gamesPauseGM(gameId!);
    await fetchStatus();
  };

  const resume = async () => {
    await gamesResumeGM(gameId!);
    await fetchStatus();
  };

  return { status, isLoading, error, refetch: fetchStatus, pause, resume };
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
      const data = await gamesSwayStory(gameId, direction);
      setLastSway(data);
      return data;
    } catch (e: any) {
      setError(e.message);
      return null;
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  return { sway, lastSway, isLoading, error };
}
