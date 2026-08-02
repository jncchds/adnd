import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { ToolCall } from '../../types'

export function useToolCalls(gameId: string | null) {
  const [toolCalls, setToolCalls] = useState<ToolCall[]>([])
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!gameId) return
    try {
      const tc = await api.toolCalls.pending(gameId)
      setToolCalls(tc)
      setError(null)
    } catch (e) {
      setError((e as Error).message)
    }
  }, [gameId])

  useEffect(() => {
    refresh()
    const interval = setInterval(refresh, 5000)
    return () => clearInterval(interval)
  }, [refresh])

  const confirm = useCallback(async (id: string) => {
    await api.toolCalls.confirm(id)
    setToolCalls(prev => prev.filter(tc => tc.id !== id))
  }, [])

  const decline = useCallback(async (id: string, reason?: string) => {
    await api.toolCalls.decline(id, reason)
    setToolCalls(prev => prev.filter(tc => tc.id !== id))
  }, [])

  return { toolCalls, error, refresh, confirm, decline }
}
