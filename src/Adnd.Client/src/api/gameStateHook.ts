import { useState, useEffect, useCallback, useRef } from 'react';
import { api, GameDetail, GMStatusResponse } from './client';
import { useGameHub } from './hubHook';

export interface PlayerState {
  id: string;
  userId: string;
  characterName: string;
  status: 'Active' | 'Disconnected';
  leftAt?: string;
}

export interface GameGameState {
  gameStatus: string;
  gmStatus: string;
  players: PlayerState[];
  lastGMAction: { action: string; at: string } | null;
  isLoading: boolean;
  error: string | null;
  isReconnecting: boolean;
  refreshState: () => Promise<void>;
  reconnect: () => Promise<void>;
}

export function useGameGameState(gameId: string | undefined): GameGameState {
  const [gameStatus, setGameStatus] = useState<string>('Created');
  const [gmStatus, setGmStatus] = useState<string>('Idle');
  const [players, setPlayers] = useState<PlayerState[]>([]);
  const [lastGMAction, setLastGMAction] = useState<{ action: string; at: string } | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isReconnecting, setIsReconnecting] = useState(false);

  const hub = useGameHub();
  const prevGMStatusRef = useRef<string>('Idle');

  // Fetch game state from API
  const refreshState = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const game = await api.getGame(gameId) as GameDetail;
      setGameStatus(game.status);

      const gmStatusResp = await api.getGMStatus(gameId) as GMStatusResponse;
      setGmStatus(gmStatusResp.status);
      setLastGMAction(gmStatusResp.lastAction ? {
        action: gmStatusResp.lastAction,
        at: gmStatusResp.lastActionAt || ''
      } : null);
    } catch (err: any) {
      setError(err.message || 'Failed to load game state');
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  // Initial fetch
  useEffect(() => {
    refreshState();
  }, [refreshState]);

  // Subscribe to SignalR events
  useEffect(() => {
    if (!hub) return;

    hub.on('GMStatusChanged', (data: { gameId: string; status: string; lastAction: string; changedAt: string }) => {
      setGmStatus(data.status);
      setLastGMAction({ action: data.lastAction, at: data.changedAt });

      // Track GM going from Idle to Running (auto-narrate triggered)
      if (prevGMStatusRef.current === 'Idle' && data.status === 'Running') {
        refreshState();
      }
      prevGMStatusRef.current = data.status;
    });

    hub.on('GameStatusChanged', (data: { gameId: string; status: string }) => {
      setGameStatus(data.status);
    });

    hub.on('PlayerJoined', (data: { playerId: string; userId: string; characterName: string; gameId: string }) => {
      setPlayers(prev => {
        const exists = prev.find(p => p.id === data.playerId);
        if (exists) return prev;
        return [...prev, { id: data.playerId, userId: data.userId, characterName: data.characterName, status: 'Active' } as PlayerState];
      });
    });

    hub.on('PlayerLeft', (data: { playerId: string; gameId: string }) => {
      setPlayers(prev => prev.filter(p => p.id !== data.playerId));
    });

    hub.on('PlayerDisconnected', (data: { playerId: string; userId: string; characterName: string; gameId: string; disconnectedAt: string }) => {
      setPlayers(prev => prev.map(p =>
        p.id === data.playerId ? { ...p, status: 'Disconnected', leftAt: data.disconnectedAt } : p
      ));
    });

    hub.on('PlayerReconnected', (data: { playerId: string; userId: string; characterName: string }) => {
      setPlayers(prev => prev.map(p =>
        p.id === data.playerId ? { ...p, status: 'Active', leftAt: undefined } : p
      ));
    });

    hub.on('Reconnected', () => {
      setIsReconnecting(false);
      refreshState();
    });

    hub.on('Reconnecting', () => {
      setIsReconnecting(true);
    });

    return () => {
      hub.off('GMStatusChanged');
      hub.off('GameStatusChanged');
      hub.off('PlayerJoined');
      hub.off('PlayerLeft');
      hub.off('PlayerDisconnected');
      hub.off('PlayerReconnected');
      hub.off('Reconnected');
      hub.off('Reconnecting');
    };
  }, [hub, refreshState]);

  const reconnect = useCallback(async () => {
    if (hub) {
      setIsReconnecting(true);
      await hub.connect();
    }
  }, [hub]);

  return {
    gameStatus,
    gmStatus,
    players,
    lastGMAction,
    isLoading,
    error,
    isReconnecting,
    refreshState,
    reconnect,
  };
}
