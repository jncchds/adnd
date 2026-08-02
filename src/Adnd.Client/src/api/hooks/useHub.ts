import { useRef, useCallback, useMemo, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { api } from '../client'

export interface HubState {
  isConnected: boolean
  error: Error | null
}

export function useGameHub() {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const [state, setState] = useState<HubState>({ isConnected: false, error: null })

  const connect = useCallback(async (gameId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('JoinGameGroup', gameId)
      return
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/gamehub', {
        accessTokenFactory: () => api.getToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.None)
      .build()

    connection.onreconnected(() => {
      setState(s => ({ ...s, isConnected: true, error: null }))
      connection.invoke('JoinGameGroup', gameId).catch(console.error)
    })

    connection.onclose(err => {
      setState(s => ({ ...s, isConnected: false, error: err ?? null }))
    })

    connectionRef.current = connection

    try {
      await connection.start()
      await connection.invoke('JoinGameGroup', gameId)
      setState({ isConnected: true, error: null })
    } catch (err) {
      setState({ isConnected: false, error: err as Error })
    }
  }, [])

  const disconnect = useCallback(async () => {
    const connection = connectionRef.current
    if (!connection) return

    // Clear the ref before awaiting stop(), and only if it still points at the connection
    // we are stopping. Under StrictMode the effect re-runs and assigns a NEW connection
    // while stop() is in flight; nulling unconditionally afterwards discarded that new
    // connection, leaving invoke() throwing "Hub not connected" over a live socket.
    if (connectionRef.current === connection) connectionRef.current = null

    try {
      await connection.stop()
    } finally {
      setState(s => (connectionRef.current === null ? { isConnected: false, error: null } : s))
    }
  }, [])

  // `unknown[]` rather than `any[]`: callers still declare the payload type they expect,
  // but TypeScript no longer silently accepts a mismatched handler signature. (The repo
  // carries eslint-disable comments for no-explicit-any but has no eslint installed, so
  // nothing was actually enforcing this.)
  const on = useCallback((event: string, handler: (...args: never[]) => void) => {
    connectionRef.current?.on(event, handler as (...args: unknown[]) => void)
  }, [])

  const off = useCallback((event: string, handler?: (...args: never[]) => void) => {
    if (handler) connectionRef.current?.off(event, handler as (...args: unknown[]) => void)
    else connectionRef.current?.off(event)
  }, [])

  const invoke = useCallback(async (method: string, ...args: unknown[]) => {
    if (!connectionRef.current) throw new Error('Hub not connected')
    return connectionRef.current.invoke(method, ...args)
  }, [])

  const waitForConnection = useCallback(async () => {
    const conn = connectionRef.current
    if (!conn || conn.state === signalR.HubConnectionState.Connected) return
    await new Promise<void>((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error('Connection timeout')), 10000)
      const check = () => {
        if (conn.state === signalR.HubConnectionState.Connected) {
          clearTimeout(timeout)
          resolve()
        } else setTimeout(check, 100)
      }
      check()
    })
  }, [])

  // Memoized so the returned object is referentially stable. It used to be a fresh literal
  // on every render, so effects depending on `hub` tore down and re-registered every
  // SignalR handler on each keystroke.
  return useMemo(
    () => ({ ...state, connect, disconnect, on, off, invoke, waitForConnection }),
    [state, connect, disconnect, on, off, invoke, waitForConnection],
  )
}

export type GameHub = ReturnType<typeof useGameHub>
