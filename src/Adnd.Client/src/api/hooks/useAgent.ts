import { useState, useCallback, useEffect } from 'react';
import { api } from '../client';
import type { PendingCallsResponse, ToolCallInfo, ToolCallConfirmationResponse, PlayerRollConfirmationResponse } from '../../types';

// ==================== Tool Call Types ====================

export interface PendingToolCall extends ToolCallInfo {
  argumentsParsed?: Record<string, unknown>;
}

export interface ToolCallEvent {
  id: string;
  toolName: string;
  outputMessage: string;
  requiresConfirmation: boolean;
  timestamp: string;
}

export interface PlayerRollEvent {
  toolCallId: string;
  skill: string;
  formula: string;
  dc: number;
  context: string;
  optional: boolean;
}

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

// ==================== Tool Call Hooks ====================

export function useToolCalls(gameId: string | undefined) {
  const [pendingCalls, setPendingCalls] = useState<PendingToolCall[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchPending = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getPendingToolCalls(gameId);
      const parsed = (data as ToolCallInfo[]).map(call => ({
        ...call,
        argumentsParsed: call.arguments ? JSON.parse(call.arguments) : undefined,
      }));
      setPendingCalls(parsed);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  const confirmToolCall = useCallback(async (toolCallId: string, approved: boolean): Promise<ToolCallConfirmationResponse | null> => {
    if (!gameId) return null;
    try {
      const result = await api.confirmToolCall(gameId, toolCallId, approved);
      await fetchPending();
      return result as ToolCallConfirmationResponse;
    } catch (e: any) {
      setError(e.message);
      return null;
    }
  }, [gameId, fetchPending]);

  const confirmPlayerRoll = useCallback(async (toolCallId: string): Promise<PlayerRollConfirmationResponse | null> => {
    if (!gameId) return null;
    try {
      const result = await api.confirmPlayerRoll(gameId, toolCallId);
      return result as PlayerRollConfirmationResponse;
    } catch (e: any) {
      setError(e.message);
      return null;
    }
  }, [gameId]);

  const declinePlayerRoll = useCallback(async (toolCallId: string) => {
    if (!gameId) return;
    try {
      await api.declinePlayerRoll(gameId, toolCallId);
      await fetchPending();
    } catch (e: any) {
      setError(e.message);
    }
  }, [gameId, fetchPending]);

  useEffect(() => {
    fetchPending();
  }, [fetchPending]);

  return {
    pendingCalls,
    isLoading,
    error,
    refetch: fetchPending,
    confirmToolCall,
    confirmPlayerRoll,
    declinePlayerRoll,
  };
}
