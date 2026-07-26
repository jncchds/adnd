import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { NPC } from '../../types'

export function useNPCs(gameId: string | null) {
  const [npcs, setNpcs] = useState<NPC[]>([])
  const [loading, setLoading] = useState(false)

  const refresh = useCallback(async () => {
    if (!gameId) return
    setLoading(true)
    try {
      const ns = await api.npcs.list(gameId)
      setNpcs(ns)
    } catch { /* handled silently */ } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => { refresh() }, [refresh])

  return { npcs, setNpcs, loading, refresh }
}
