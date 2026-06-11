import { useState, useEffect, useCallback } from 'react';
import { gamesGetPlotThreads, gamesCreatePlotThread, gamesUpdatePlotThread } from '../../api/games/gameApi';

export function usePlotThreads(gameId: string | undefined) {
  const [threads, setThreads] = useState<{ id: string; title: string; description: string; status: string }[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchThreads = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await gamesGetPlotThreads(gameId);
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
    await gamesCreatePlotThread(gameId!, title, description);
    await fetchThreads();
  };

  const updateThread = async (threadId: string, updates: { title?: string; description?: string; status?: string }) => {
    await gamesUpdatePlotThread(threadId, updates);
    await fetchThreads();
  };

  return { threads, isLoading, error, refetch: fetchThreads, createThread, updateThread };
}
