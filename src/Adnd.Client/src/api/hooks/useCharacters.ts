import { useState, useEffect, useCallback } from 'react'
import type { Character } from '../../types'
import { api } from '../client'

export function useCharacter(characterId: string | null) {
  const [character, setCharacter] = useState<Character | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // silent: a live update re-fetches without swapping the sheet for a spinner.
  const refresh = useCallback(async (opts?: { silent?: boolean }) => {
    if (!characterId) return
    if (!opts?.silent) setLoading(true)
    setError(null)
    try {
      const c = await api.characters.get(characterId)
      setCharacter(c)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      if (!opts?.silent) setLoading(false)
    }
  }, [characterId])

  useEffect(() => { refresh() }, [refresh])

  return { character, setCharacter, loading, error, refresh }
}
