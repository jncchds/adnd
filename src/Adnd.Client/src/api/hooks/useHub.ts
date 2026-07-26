import { useState, useCallback, useRef } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { HubMethods } from './hubMethods';
import { createHubMethods } from './hubMethods';

export function useGameHub() {
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const hubRef = useRef<HubConnection | null>(null);

  const connect = useCallback(async (gameId: string, token: string) => {
    if (!hubRef.current) {
      hubRef.current = new HubConnectionBuilder()
        .withUrl(`/gamehub`, {
          accessTokenFactory: () => Promise.resolve(token),
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.None)
        .build();
    }
    try {
      await hubRef.current.start();
      await hubRef.current.invoke('JoinGameGroup', gameId);
      setIsConnected(true);
      setError(null);
    } catch (e: any) {
      setError(`Failed to connect: ${e.message}`);
      setIsConnected(false);
    }
  }, []);

  const disconnect = useCallback(async () => {
    if (hubRef.current) {
      await hubRef.current.stop();
      hubRef.current = null;
      setIsConnected(false);
    }
  }, []);

  const on = useCallback((event: string, handler: (...args: any[]) => void) => {
    if (hubRef.current) hubRef.current.on(event, handler);
  }, []);

  const off = useCallback((event: string, handler?: ((...args: any[]) => void) | undefined) => {
    if (hubRef.current && handler) hubRef.current.off(event, handler);
    else if (hubRef.current) hubRef.current.off(event);
  }, []);

  const invoke = useCallback(async (method: string, ...args: any[]) => {
    if (!hubRef.current) throw new Error('Hub connection not established');
    return hubRef.current.invoke(method, ...args);
  }, []);

  const waitForConnection = useCallback(async () => {
    if (isConnected) return true;
    return new Promise<boolean>((resolve) => {
      const check = setInterval(() => {
        if (isConnected) { clearInterval(check); resolve(true); }
      }, 100);
      setTimeout(() => { clearInterval(check); resolve(false); }, 10000);
    });
  }, [isConnected]);

  const methodsRef = useRef<HubMethods | null>(null);
  if (hubRef.current && !methodsRef.current) {
    methodsRef.current = createHubMethods(hubRef as React.MutableRefObject<HubConnection | null>);
  }

  return {
    isConnected,
    error,
    connect,
    disconnect,
    on,
    off,
    invoke,
    waitForConnection,
    ...(methodsRef.current || {}),
  };
}
