import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  Box, Typography, Paper, CircularProgress, Button, Chip,
  LinearProgress, Grid, Divider, Alert,
} from '@mui/material'
import { Person as CharIcon, Add as AddIcon } from '@mui/icons-material'
import { api } from '../api/client'
import { usePlayers } from '../api/hooks/usePlayers'
import { useGame } from '../api/hooks/useGame'
import { useAuth } from '../context/AuthContext'
import type { Character, Player } from '../types'

function AttrBadge({ abbr, value }: { abbr: string; value: number }) {
  const mod = Math.floor((value - 10) / 2)
  return (
    <Box sx={{ textAlign: 'center', minWidth: 48 }}>
      <Typography variant="caption" color="text.secondary" display="block" sx={{ fontSize: 10 }}>{abbr}</Typography>
      <Typography variant="body2" fontWeight={700}>{value}</Typography>
      <Typography variant="caption" color={mod >= 0 ? 'success.main' : 'error.main'} sx={{ fontSize: 10 }}>
        {mod >= 0 ? '+' : ''}{mod}
      </Typography>
    </Box>
  )
}

function OwnCharacterCard({ character, onView }: { character: Character; onView: () => void }) {
  const hpPct = character.maxHP > 0 ? Math.max(0, Math.min(100, (character.currentHP / character.maxHP) * 100)) : 0
  const hpColor = hpPct > 50 ? 'success' : hpPct > 25 ? 'warning' : 'error'
  const attrs = character.attributes ?? {}

  return (
    <Paper sx={{ p: 2.5, border: '2px solid', borderColor: 'primary.main', position: 'relative' }}>
      <Chip label="Your Character" size="small" color="primary" sx={{ position: 'absolute', top: 12, right: 12 }} />
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <CharIcon color="primary" />
        <Typography variant="h6" fontWeight={700}>{character.name}</Typography>
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Level {character.level} {character.class} · Prof +{character.proficiencyBonus}
      </Typography>

      {/* HP */}
      <Box sx={{ mb: 1.5 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
          <Typography variant="caption" color="text.secondary">Hit Points</Typography>
          <Typography variant="caption" fontWeight={600}>{character.currentHP} / {character.maxHP}</Typography>
        </Box>
        <LinearProgress variant="determinate" value={hpPct} color={hpColor} sx={{ height: 6, borderRadius: 3 }} />
      </Box>

      {/* Attributes */}
      {Object.keys(attrs).length > 0 && (
        <>
          <Divider sx={{ mb: 1.5 }} />
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 1.5 }}>
            {Object.entries(attrs).map(([k, v]) => <AttrBadge key={k} abbr={k} value={v} />)}
          </Box>
        </>
      )}

      {/* Background */}
      {character.backgroundFeatures && (
        <Typography variant="caption" color="text.secondary" display="block" sx={{ mb: 1.5 }}>
          Feature: {character.backgroundFeatures}
          {character.backgroundSkills && ` · Skills: ${character.backgroundSkills}`}
        </Typography>
      )}

      <Button size="small" variant="outlined" onClick={onView} startIcon={<CharIcon />}>
        Full Sheet
      </Button>
    </Paper>
  )
}

function OtherCharacterCard({ character, player }: { character: Character; player: Player | undefined }) {
  return (
    <Paper sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
        <CharIcon sx={{ color: 'text.secondary', fontSize: 18 }} />
        <Typography variant="subtitle1" fontWeight={600}>{character.name}</Typography>
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
        Level {character.level} {character.class}
      </Typography>
      {player && (
        <Typography variant="caption" color="text.secondary" display="block" sx={{ mb: 1 }}>
          Played by {player.displayName ?? 'Unknown'}
        </Typography>
      )}
      {character.backgroundFeatures && (
        <Chip label={character.backgroundFeatures} size="small" variant="outlined" sx={{ mr: 0.5 }} />
      )}
      {character.backgroundSkills && (
        <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 0.5 }}>
          Skills: {character.backgroundSkills}
        </Typography>
      )}
    </Paper>
  )
}

export default function GameCharactersPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { user } = useAuth()
  const { loading: gameLoading } = useGame(gameId ?? null)
  const { players } = usePlayers(gameId ?? null)
  const [characters, setCharacters] = useState<Character[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const myPlayer = players.find(p => p.userId === user?.id)

  useEffect(() => {
    if (!gameId) return
    setLoading(true)
    api.characters.listForGame(gameId)
      .then(setCharacters)
      .catch(e => setError((e as Error).message))
      .finally(() => setLoading(false))
  }, [gameId])

  const myCharacter = characters.find(c => c.playerId === myPlayer?.id)
  const otherCharacters = characters.filter(c => c.playerId !== myPlayer?.id)

  if (loading || gameLoading) return <Box sx={{ p: 3 }}><CircularProgress /></Box>

  return (
    <Box sx={{ p: 3, maxWidth: 900, mx: 'auto' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" fontWeight={700}>Party Characters</Typography>
        {!myCharacter && (
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => navigate(`/game/${gameId}/character/new`)}
          >
            Create My Character
          </Button>
        )}
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {characters.length === 0 && (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary" sx={{ mb: 2 }}>No characters have been created yet.</Typography>
          {!myCharacter && (
            <Button variant="contained" startIcon={<AddIcon />}
              onClick={() => navigate(`/game/${gameId}/character/new`)}>
              Create My Character
            </Button>
          )}
        </Paper>
      )}

      {characters.length > 0 && (
        <Grid container spacing={2}>
          {myCharacter && (
            <Grid size={{ xs: 12, md: 6 }}>
              <OwnCharacterCard character={myCharacter} onView={() => navigate(`/game/${gameId}/character/${myCharacter.id}`)} />
            </Grid>
          )}
          {otherCharacters.map(c => {
            const player = players.find(p => p.id === c.playerId)
            return (
              <Grid size={{ xs: 12, sm: 6, md: 4 }} key={c.id}>
                <OtherCharacterCard character={c} player={player} />
              </Grid>
            )
          })}
        </Grid>
      )}
    </Box>
  )
}
