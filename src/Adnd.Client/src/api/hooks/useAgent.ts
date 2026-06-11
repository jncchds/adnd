import { useState, useCallback, useEffect } from 'react';
import { api } from '../client';
import type { PendingCallsResponse } from '../../types';

export function usePendingCalls(gameId: string | undefined) {
  const [calls, setCalls] = useState<PendingCallsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCalls = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getPendingAgentCalls(gameId);
      setCalls(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchCalls();
  }, [fetchCalls]);

  return {
    calls,
    isLoading,
    error,
    refetch: fetchCalls,
  };
}
