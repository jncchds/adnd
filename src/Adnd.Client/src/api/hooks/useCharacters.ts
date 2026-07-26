import { useState, useEffect, useCallback } from 'react'
import { api } from '../client'

export function useCharacter(characterId: string | null) {
  const [character, setCharacter] = useState<Record<string, unknown> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!characterId) return
    setLoading(true)
    setError(null)
    try {
      const c = await api.characters.get(characterId)
      setCharacter(c)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [characterId])

  useEffect(() => { refresh() }, [refresh])

  return { character, setCharacter, loading, error, refresh }
}
