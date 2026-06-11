import { useState, useCallback, useEffect } from 'react';
import { api } from '../client';
import type { GameDetail, GMStatusResponse } from '../../types';

export function useGameGameState(gameId: string | undefined) {
  const [gameState, setGameState] = useState<GameDetail | null>(null);
  const [gmStatus, setGmStatus] = useState<GMStatusResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchState = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const [game, status] = await Promise.all([
        api.getGame(gameId),
        api.getGMStatus(gameId),
      ]);
      setGameState(game);
      setGmStatus(status);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchState();
  }, [fetchState]);

  const updateGameState = async (updates: { gameState?: string; plotSeed?: string; gameParameters?: string }) => {
    if (!gameId) return;
    await api.updateGameState(gameId, updates.gameState, updates.plotSeed, updates.gameParameters);
    await fetchState();
  };

  return {
    gameState,
    gmStatus,
    isLoading,
    error,
    refetch: fetchState,
    updateGameState,
  };
}
