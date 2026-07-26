import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { Player } from '../../types'

export function usePlayers(gameId: string | null) {
  const [players, setPlayers] = useState<Player[]>([])
  const [loading, setLoading] = useState(false)

  const refresh = useCallback(async () => {
    if (!gameId) return
    setLoading(true)
    try {
      const ps = await api.games.getPlayers(gameId)
      setPlayers(ps)
    } catch { /* handled silently */ } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => { refresh() }, [refresh])

  return { players, setPlayers, loading, refresh }
}
