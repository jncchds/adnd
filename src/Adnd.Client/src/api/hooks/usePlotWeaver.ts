import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { PlotThread } from '../../types'

export function usePlotThreads(gameId: string | null) {
  const [threads, setThreads] = useState<PlotThread[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!gameId) return
    setLoading(true)
    setError(null)
    try {
      const ts = await api.plots.list(gameId)
      setThreads(ts)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => { refresh() }, [refresh])

  return { threads, loading, error, refresh }
}
