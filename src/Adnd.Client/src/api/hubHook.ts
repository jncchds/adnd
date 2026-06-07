import { useRef, useCallback, useState } from 'react';

interface HubConnection {
  isConnected: boolean;
  connect: (url: string, token?: string) => Promise<void>;
  disconnect: () => Promise<void>;
  on: (event: string, handler: (...args: any[]) => void) => void;
  off: (event: string, handler?: (...args: any[]) => void) => void;
  invoke: (method: string, ...args: any[]) => Promise<any>;
  get connectionId(): string | null;
}

// Whisper response from server
export interface WhisperResponse {
  id: string;
  fromPlayerId: string;
  fromCharacter: string;
  fromRole: string;
  content: string;
  type: number;
  targets: string;
  createdAt: string;
  isSent?: boolean;
}

// Agent call events from server
export interface AgentCallStartedEvent {
  id: string;
  fromAgent: number;
  toAgent: number;
  action: number;
  createdAt: string;
}

export interface AgentCallCompletedEvent {
  id: string;
  fromAgent: number;
  toAgent: number;
  action: number;
  status: number;
  output?: string;
  outputMessage?: string;
  durationMs: number;
  completedAt: string;
  error?: string;
}

const HUB_URL = '/gamehub';

function createHubConnection(): HubConnection {
  let connection: any = null;
  let isConnected = false;
  const listeners = new Map<string, Set<(...args: any[]) => void>>();

  const hub: HubConnection = {
    get isConnected() {
      return isConnected;
    },

    get connectionId() {
      return connection?.connectionId ?? null;
    },

    connect: async (url: string, token?: string) => {
      if (connection?.state === 1) {
        isConnected = true;
        return;
      }

      const signalR = await import('@microsoft/signalr');

      const builder = new signalR.HubConnectionBuilder()
        .withUrl(url, {
          accessTokenFactory: () => token || '',
          skipNegotiation: true,
          transport: signalR.HttpTransportType.WebSockets,
        })
        .withAutomaticReconnect()
        .build();

      builder.onclose(() => {
        isConnected = false;
      });

      builder.onreconnecting(() => {
        isConnected = false;
      });

      builder.onreconnected(() => {
        isConnected = true;
      });

      await builder.start();
      connection = builder;
      isConnected = true;
    },

    disconnect: async () => {
      if (connection) {
        await connection.stop();
        connection = null;
        isConnected = false;
      }
    },

    on: (event: string, handler: (...args: any[]) => void) => {
      if (!listeners.has(event)) {
        listeners.set(event, new Set());
      }
      listeners.get(event)!.add(handler);

      if (connection) {
        connection.on(event, (...args: any[]) => {
          listeners.get(event)?.forEach((h) => h(...args));
        });
      }
    },

    off: (event: string, handler?: (...args: any[]) => void) => {
      if (handler && listeners.has(event)) {
        listeners.get(event)!.delete(handler);
      } else if (connection) {
        connection.off(event);
      }
    },

    invoke: async (method: string, ...args: any[]) => {
      if (!connection || connection.state !== 1) {
        throw new Error('Hub connection is not active');
      }
      return connection.invoke(method, ...args);
    },
  };

  return hub;
}

// Custom hook for connecting to the game hub
export function useGameHub() {
  const hubRef = useRef<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  if (!hubRef.current) {
    hubRef.current = createHubConnection();
  }

  const connect = useCallback(async (token?: string) => {
    try {
      await hubRef.current!.connect(HUB_URL, token);
      setIsConnected(true);
    } catch (e) {
      console.error('Failed to connect to game hub:', e);
      setIsConnected(false);
    }
  }, []);

  const disconnect = useCallback(async () => {
    await hubRef.current?.disconnect();
    setIsConnected(false);
  }, []);

  const on = useCallback((event: string, handler: (...args: any[]) => void) => {
    hubRef.current?.on(event, handler);
  }, []);

  const off = useCallback((event: string, handler?: (...args: any[]) => void) => {
    hubRef.current?.off(event, handler);
  }, []);

  const invoke = useCallback(async (method: string, ...args: any[]) => {
    return hubRef.current?.invoke(method, ...args);
  }, []);

  return {
    isConnected,
    connect,
    disconnect,
    on,
    off,
    invoke,
    // New hub methods
    sendWhisper: async (targets: string, content: string) => {
      return hubRef.current?.invoke('SendWhisper', targets, content);
    },
    sendGMWhisper: async (targetPlayerId: string, content: string) => {
      return hubRef.current?.invoke('SendGMWhisper', targetPlayerId, content);
    },
    callAgent: async (fromAgent: number, toAgent: number, action: number, input: string, sessionId?: string) => {
      return hubRef.current?.invoke('CallAgent', fromAgent, toAgent, action, input, sessionId);
    },
    getWhisperHistory: async (gameId: string, limit = 50) => {
      return hubRef.current?.invoke('GetWhisperHistory', gameId, limit);
    },
    getAgentCallHistory: async (gameId: string, fromAgent?: number, action?: number, limit = 50) => {
      return hubRef.current?.invoke('GetAgentCallHistory', gameId, fromAgent, action, limit);
    },
    getAgentCall: async (callId: string) => {
      return hubRef.current?.invoke('GetAgentCall', callId);
    },
    updateGameState: async (gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) => {
      return hubRef.current?.invoke('UpdateGameState', gameId, gameState, plotSeed, gameParameters);
    },
    getSystems: async (gameId: string) => {
      return hubRef.current?.invoke('GetSystems', gameId);
    },
    createSystem: async (gameId: string, name: string, jsonDefinition: string) => {
      return hubRef.current?.invoke('CreateSystem', gameId, name, jsonDefinition);
    },
  };
}
