import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'
import type { PlotThread } from '../../types'

export function usePlotThreads(gameId: string | null) {
  const [threads, setThreads] = useState<PlotThread[]>([])
  const [loading, setLoading] = useState(false)

  const refresh = useCallback(async () => {
    if (!gameId) return
    setLoading(true)
    try {
      const ts = await api.plots.list(gameId)
      setThreads(ts)
    } catch { /* handled silently */ } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => { refresh() }, [refresh])

  return { threads, loading, refresh }
}
