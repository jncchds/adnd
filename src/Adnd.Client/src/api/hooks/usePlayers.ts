import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { Player } from '../../types'

export function usePlayers(gameId: string | null) {
  const [players, setPlayers] = useState<Player[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!gameId) return
    setLoading(true)
    setError(null)
    try {
      const ps = await api.games.getPlayers(gameId)
      setPlayers(ps)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => { refresh() }, [refresh])

  return { players, setPlayers, loading, error, refresh }
}
