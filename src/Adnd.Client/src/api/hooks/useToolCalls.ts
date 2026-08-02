import { useState, useEffect, useCallback, useMemo } from 'react'
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

  const reroll = useCallback(async (id: string, featureId: string | null) => {
    await api.toolCalls.reroll(id, featureId)
    setToolCalls(prev => prev.filter(tc => tc.id !== id))
  }, [])

  // Two different questions share this endpoint: "may the GM roll for you?" and "you rolled,
  // do you want to spend an ability?". Rendering the second through the confirm/decline
  // banner would offer Confirm and Decline buttons that resolve nothing.
  const confirmations = useMemo(
    () => toolCalls.filter(tc => tc.status === 'AwaitingConfirmation'),
    [toolCalls])

  const rerollOffers = useMemo(
    () => toolCalls.filter(tc => tc.status === 'AwaitingReroll'),
    [toolCalls])

  return { toolCalls, confirmations, rerollOffers, error, refresh, confirm, decline, reroll }
}
