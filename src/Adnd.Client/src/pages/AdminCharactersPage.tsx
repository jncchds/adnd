import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Box, Typography, Paper, CircularProgress, Chip, Button } from '@mui/material'
import { usePlayers } from '../api/hooks/usePlayers'
import { api } from '../api/client'
import type { Character } from '../types'

export default function AdminCharactersPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const { players, loading } = usePlayers(gameId ?? null)
  const navigate = useNavigate()
  const [chars, setChars] = useState<Record<string, Character>>({})

  useEffect(() => {
    if (!gameId) return
    api.characters.listForGame(gameId)
      .then(list => {
        const map: Record<string, Character> = {}
        list.forEach(c => { map[c.playerId] = c })
        setChars(map)
      })
      .catch(() => {})
  }, [gameId])

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" fontWeight={700} sx={{ mb: 3 }}>Characters</Typography>
      {loading && <CircularProgress />}
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: 2 }}>
        {players.map(p => {
          const char = chars[p.id]
          return (
            <Paper key={p.id} sx={{ p: 2 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
                <Typography variant="subtitle2" fontWeight={600}>{p.displayName ?? 'Player'}</Typography>
                <Chip label={p.role} size="small" />
              </Box>
              {char ? (
                <>
                  <Typography variant="body2">{char.name} — Level {char.level} {char.class}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    HP: {char.currentHP}/{char.maxHP}
                  </Typography>
                  <Box sx={{ mt: 1 }}>
                    <Button size="small" onClick={() => navigate(`/game/${gameId}/character/${char.id}`)}>View Sheet</Button>
                  </Box>
                </>
              ) : (
                <Typography variant="body2" color="text.secondary">No character</Typography>
              )}
            </Paper>
          )
        })}
      </Box>
    </Box>
  )
}
