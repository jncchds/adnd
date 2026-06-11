import { useState, useCallback } from 'react';
import { combatGetCombats, combatGetCombat } from '../../api/combat/combatApi';
import type { CombatSummaryEntry, CombatLogResponse } from '../../types';

export function useCombats(gameId: string | undefined) {
  const [combats, setCombats] = useState<CombatSummaryEntry[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCombats = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await combatGetCombats(gameId);
      setCombats(data.combats);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  return {
    combats,
    isLoading,
    error,
    refetch: fetchCombats,
  };
}

export function useCombat(combatId: string | undefined, gameId: string | undefined) {
  const [combat, setCombat] = useState<CombatLogResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCombat = useCallback(async () => {
    if (!combatId || !gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await combatGetCombat(gameId, combatId);
      setCombat(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [combatId, gameId]);

  return {
    combat,
    isLoading,
    error,
    refetch: fetchCombat,
  };
}
