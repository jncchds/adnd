import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { Game } from '../../types'

export function useGame(gameId: string | null) {
  const [game, setGame] = useState<Game | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!gameId) return
    setLoading(true)
    setError(null)
    try {
      const g = await api.games.get(gameId)
      setGame(g)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => { refresh() }, [refresh])

  return { game, setGame, loading, error, refresh }
}

export function useGames() {
  const [games, setGames] = useState<Game[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const gs = await api.games.list()
      setGames(gs)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  return { games, loading, error, refresh }
}
