import { useRef, useCallback, useState } from 'react'
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
    if (connectionRef.current) {
      await connectionRef.current.stop()
      connectionRef.current = null
      setState({ isConnected: false, error: null })
    }
  }, [])

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const on = useCallback((event: string, handler: (...args: any[]) => void) => {
    connectionRef.current?.on(event, handler)
  }, [])

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const off = useCallback((event: string, handler?: (...args: any[]) => void) => {
    if (handler) connectionRef.current?.off(event, handler)
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

  return { ...state, connect, disconnect, on, off, invoke, waitForConnection }
}

export type GameHub = ReturnType<typeof useGameHub>
