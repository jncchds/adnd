import { useState, useCallback } from 'react';
import { api } from '../client';
import type { DiceHistoryEntry } from '../../types';

export function useDiceHistory(gameId: string | undefined) {
  const [rolls, setRolls] = useState<DiceHistoryEntry[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchRolls = useCallback(async (params?: {
    sessionId?: string;
    playerId?: string;
    limit?: number;
    sortBy?: string;
  }) => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getDiceHistory(gameId, params);
      setRolls(data.rolls);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  return {
    rolls,
    isLoading,
    error,
    refetch: fetchRolls,
  };
}
