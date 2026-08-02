import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { NPC } from '../../types'

export function useNPCs(gameId: string | null) {
  const [npcs, setNpcs] = useState<NPC[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!gameId) return
    setLoading(true)
    setError(null)
    try {
      const ns = await api.npcs.list(gameId)
      setNpcs(ns)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => { refresh() }, [refresh])

  return { npcs, setNpcs, loading, error, refresh }
}
