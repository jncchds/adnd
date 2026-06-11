import { useState, useCallback, useEffect } from 'react';
import { api } from '../client';
import type { PlotThreadResponse, PlotReviewResponse } from '../../types';

export function usePlotWeaver(gameId: string | undefined) {
  const [threads, setThreads] = useState<PlotThreadResponse[]>([]);
  const [reviews, setReviews] = useState<PlotReviewResponse[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchThreads = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getPlotWeaverThreads(gameId);
      setThreads(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  const fetchReviews = useCallback(async () => {
    if (!gameId) return;
    try {
      const data = await api.getPlotReviewHistory(gameId);
      setReviews(data);
    } catch (e: any) {
      console.error('Failed to fetch plot reviews', e);
    }
  }, [gameId]);

  const triggerReview = useCallback(async (context?: string) => {
    if (!gameId) return null;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.triggerPlotReview(gameId, context);
      setReviews(prev => [data, ...prev]);
      await fetchThreads();
      return data;
    } catch (e: any) {
      setError(e.message);
      return null;
    } finally {
      setIsLoading(false);
    }
  }, [gameId, fetchThreads]);

  const adjustMomentum = useCallback(async (threadId: string, delta: number, reason: string) => {
    if (!gameId) return;
    try {
      await api.adjustThreadMomentum(gameId, threadId, delta, reason);
      await fetchThreads();
    } catch (e: any) {
      console.error('Failed to adjust momentum', e);
    }
  }, [gameId, fetchThreads]);

  useEffect(() => {
    fetchThreads();
  }, [fetchThreads]);

  const detectOpportunities = useCallback(async () => {
    if (!gameId) return [];
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.detectOpportunities(gameId);
      await fetchThreads();
      return data;
    } catch (e: any) {
      setError(e.message);
      return [];
    } finally {
      setIsLoading(false);
    }
  }, [gameId, fetchThreads]);

  return {
    threads,
    reviews,
    isLoading,
    error,
    refetch: fetchThreads,
    fetchReviews,
    triggerReview,
    adjustMomentum,
    detectOpportunities,
  };
}
