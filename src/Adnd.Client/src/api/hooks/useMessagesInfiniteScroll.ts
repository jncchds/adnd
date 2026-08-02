import { useState, useCallback, useRef } from 'react'
import { api } from '../client'
import type { Message } from '../../types'

export function useMessagesInfiniteScroll(sessionId: string | null) {
  const [messages, setMessages] = useState<Message[]>([])
  const [loading, setLoading] = useState(false)
  // Starts false, not true — otherwise "Load older messages" flashes before the initial
  // page load has told us whether there's actually anything older to load.
  const [hasMore, setHasMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const cursorRef = useRef<string | null>(null)
  const seenIds = useRef(new Set<string>())

  const loadInitial = useCallback(async () => {
    if (!sessionId) return
    setLoading(true)
    setError(null)
    cursorRef.current = null
    seenIds.current.clear()
    try {
      const page = await api.games.getMessages(sessionId)
      const unique = page.items.filter(m => !seenIds.current.has(m.id))
      unique.forEach(m => seenIds.current.add(m.id))
      setMessages(unique)
      setHasMore(page.hasMore)
      cursorRef.current = page.nextCursor
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [sessionId])

  const loadOlder = useCallback(async () => {
    if (!sessionId || loading || !hasMore) return
    setLoading(true)
    setError(null)
    try {
      const page = await api.games.getMessages(sessionId, cursorRef.current ?? undefined)
      const unique = page.items.filter(m => !seenIds.current.has(m.id))
      unique.forEach(m => seenIds.current.add(m.id))
      setMessages(prev => [...unique, ...prev])
      setHasMore(page.hasMore)
      cursorRef.current = page.nextCursor
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [sessionId, loading, hasMore])

  const appendLive = useCallback((msg: Message) => {
    if (seenIds.current.has(msg.id)) return
    seenIds.current.add(msg.id)
    setMessages(prev => [...prev, msg])
  }, [])

  /**
   * A prompt resolving into its outcome. The row keeps its id and its place in the log, so
   * "the GM asks you to roll" becomes the roll where it already sat rather than scrolling
   * away and reappearing at the bottom.
   */
  const replaceLive = useCallback((msg: Message) => {
    setMessages(prev => {
      const index = prev.findIndex(m => m.id === msg.id)
      if (index < 0) {
        // The prompt was never in this client's window — treat the outcome as new.
        if (seenIds.current.has(msg.id)) return prev
        seenIds.current.add(msg.id)
        return [...prev, msg]
      }
      const next = [...prev]
      next[index] = msg
      return next
    })
  }, [])

  const removeLive = useCallback((messageId: string) => {
    seenIds.current.delete(messageId)
    setMessages(prev => prev.filter(m => m.id !== messageId))
  }, [])

  return { messages, loading, hasMore, error, loadInitial, loadOlder, appendLive, replaceLive, removeLive }
}
