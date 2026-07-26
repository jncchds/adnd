import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { ToolCall } from '../../types'

export function useToolCalls(gameId: string | null) {
  const [toolCalls, setToolCalls] = useState<ToolCall[]>([])

  const refresh = useCallback(async () => {
    if (!gameId) return
    try {
      const tc = await api.toolCalls.pending(gameId)
      setToolCalls(tc)
    } catch { /* handled silently */ }
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

  return { toolCalls, refresh, confirm }
}
