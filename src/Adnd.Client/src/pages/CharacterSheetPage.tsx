import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, Paper, Tabs, Tab, CircularProgress, Alert,
  TextField, Button, Chip, Divider, Grid, LinearProgress,
} from '@mui/material'
import { useCharacter } from '../api/hooks/useCharacters'
import { useGameHub } from '../api/hooks/useHub'
import { api } from '../api/client'
import type { Character, CharacterOptions, InventoryItem } from '../types'

// Mirrors ClassRules.XpThresholds on the server: total XP to reach each level.
const XP_THRESHOLDS = [0, 300, 900, 2700, 6500, 14000, 23000, 34000, 48000, 64000,
  85000, 100000, 120000, 140000, 165000, 195000, 225000, 265000, 305000, 355000]

function stacks(inventory: Character['inventory']): InventoryItem[] {
  return (inventory ?? []).map(i => (typeof i === 'string' ? { name: i, quantity: 1 } : i))
}

function AttrCard({ abbr, value }: { abbr: string; value: number }) {
  const mod = Math.floor((value - 10) / 2)
  return (
    <Paper sx={{ p: 1.5, textAlign: 'center', minWidth: 72 }}>
      <Typography variant="caption" color="text.secondary" display="block">{abbr}</Typography>
      <Typography variant="h5" fontWeight={700}>{value}</Typography>
      <Typography variant="body2" color={mod >= 0 ? 'success.main' : 'error.main'}>
        {mod >= 0 ? '+' : ''}{mod}
      </Typography>
    </Paper>
  )
}

export default function CharacterSheetPage() {
  // Routed as /game/:id/character/:characterId — `id` is the game, not the sheet.
  const { id: gameId, characterId } = useParams<{ id: string; characterId: string }>()
  const { character, loading, error, setCharacter, refresh } = useCharacter(characterId ?? null)
  const [tab, setTab] = useState(0)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [options, setOptions] = useState<CharacterOptions | null>(null)
  const [optionsError, setOptionsError] = useState<string | null>(null)
  const [customText, setCustomText] = useState<string | null>(null)
  const [customError, setCustomError] = useState<string | null>(null)
  const hub = useGameHub()

  useEffect(() => {
    api.characters.options()
      .then(o => { setOptions(o); setOptionsError(null) })
      .catch(e => setOptionsError((e as Error).message))
  }, [])

  // The GM changes this sheet during play — damage, loot, a spell slot, a level — and pushes
  // CharacterUpdated. Without listening, an open sheet showed whatever was true when it loaded.
  useEffect(() => {
    if (!gameId) return
    hub.connect(gameId).catch(() => { /* surfaced via hub.error below */ })
    return () => { hub.disconnect().catch(() => { /* unmounting anyway */ }) }
  }, [gameId]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const handle = ({ characterId: changed }: { characterId: string }) => {
      if (changed === characterId) refresh({ silent: true })
    }
    hub.on('CharacterUpdated', handle)
    return () => hub.off('CharacterUpdated', handle)
  }, [hub, characterId, refresh])

  // Only who the character is can be edited here. Everything mechanical is the GM's.
  const save = async () => {
    if (!characterId || !character) return
    setSaving(true)
    setSaveError(null)
    try {
      const updated = await api.characters.update(characterId, {
        name: character.name,
        background: character.background,
        backgroundSkills: character.backgroundSkills,
        backgroundFeatures: character.backgroundFeatures,
        backstory: character.backstory,
        customFields: character.customFields,
      })
      setCharacter(updated)
    } catch (e) { setSaveError((e as Error).message) }
    finally { setSaving(false) }
  }

  if (loading) return <Box sx={{ p: 3 }}><CircularProgress /></Box>
  if (error) return <Box sx={{ p: 3 }}><Alert severity="error">{error}</Alert></Box>
  if (!character) return <Box sx={{ p: 3 }}><Typography>Character not found</Typography></Box>

  const attrs = character.attributes ?? {}
  const skills = character.skills as Record<string, unknown> ?? {}
  const inventory = stacks(character.inventory)
  const spells = character.spells as Record<string, unknown> ?? {}
  const slots = Object.entries(character.spellSlots ?? {}).sort(([a], [b]) => Number(a) - Number(b))
  const features = character.features ?? []
  const conditions = character.conditions ?? []
  const xp = character.experience ?? 0
  const levelFloor = XP_THRESHOLDS[character.level - 1] ?? 0
  const nextLevelAt = XP_THRESHOLDS[character.level] ?? null
  const xpPct = nextLevelAt == null ? 100 : Math.min(100, ((xp - levelFloor) / (nextLevelAt - levelFloor)) * 100)

  return (
    <Box sx={{ p: 3, maxWidth: 900, mx: 'auto' }}>
      {/* Header */}
      <Box sx={{ mb: 3 }}>
        <Typography variant="h4" fontWeight={700}>{character.name}</Typography>
        <Typography variant="subtitle1" color="text.secondary">
          Level {character.level} {character.race ? `${character.race} ` : ''}{character.class} — HP: {character.currentHP}/{character.maxHP}
        </Typography>
        {conditions.length > 0 && (
          <Box sx={{ display: 'flex', gap: 0.5, mt: 1, flexWrap: 'wrap' }}>
            {conditions.map(c => <Chip key={c} label={c} size="small" color="warning" />)}
          </Box>
        )}
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
          Hit points, inventory, spell slots, abilities and level are kept by the GM as the story unfolds.
        </Typography>
      </Box>

      {saveError && <Alert severity="error" sx={{ mb: 2 }}>{saveError}</Alert>}
      {hub.error && <Alert severity="warning" sx={{ mb: 2 }}>Live updates are unavailable: {hub.error.message}</Alert>}

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab label="Stats" />
        <Tab label="Skills" />
        <Tab label="Inventory" />
        <Tab label="Spells" />
        <Tab label="Background" />
        <Tab label="Features" />
        <Tab label="Custom" />
      </Tabs>

      {/* Stats */}
      {tab === 0 && (
        <Box>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 2 }}>
            {Object.entries(attrs).map(([k, v]) => (
              <AttrCard key={k} abbr={k} value={v} />
            ))}
          </Box>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6, sm: 3 }}>
              <Paper sx={{ p: 1.5, textAlign: 'center' }}>
                <Typography variant="caption" color="text.secondary">HP</Typography>
                <Typography variant="h6">{character.currentHP}/{character.maxHP}</Typography>
              </Paper>
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <Paper sx={{ p: 1.5, textAlign: 'center' }}>
                <Typography variant="caption" color="text.secondary">Level</Typography>
                <Typography variant="h6">{character.level}</Typography>
              </Paper>
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <Paper sx={{ p: 1.5, textAlign: 'center' }}>
                <Typography variant="caption" color="text.secondary">Prof. Bonus</Typography>
                <Typography variant="h6">+{character.proficiencyBonus}</Typography>
              </Paper>
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <Paper sx={{ p: 1.5, textAlign: 'center' }}>
                <Typography variant="caption" color="text.secondary">Experience</Typography>
                <Typography variant="h6">{xp}</Typography>
                <LinearProgress variant="determinate" value={xpPct} sx={{ mt: 0.5, height: 4, borderRadius: 2 }} />
                <Typography variant="caption" color="text.secondary">
                  {nextLevelAt == null ? 'Max level' : `Level ${character.level + 1} at ${nextLevelAt}`}
                </Typography>
              </Paper>
            </Grid>
          </Grid>
        </Box>
      )}

      {/* Skills */}
      {tab === 1 && (
        <Box>
          {Object.entries(skills).map(([skill, val]) => (
            <Box key={skill} sx={{ display: 'flex', alignItems: 'center', py: 0.5 }}>
              <Typography variant="body2" sx={{ flex: 1 }}>{skill}</Typography>
              <Chip label={String(val)} size="small" />
            </Box>
          ))}
          {Object.keys(skills).length === 0 && (
            <Typography color="text.secondary">No skills defined</Typography>
          )}
        </Box>
      )}

      {/* Inventory */}
      {tab === 2 && (
        <Box>
          {inventory.map((item, i) => (
            <Box key={`${item.name}-${i}`} sx={{ py: 0.5, borderBottom: '1px solid', borderColor: 'divider', display: 'flex' }}>
              <Typography variant="body2" sx={{ flex: 1 }}>{item.name}</Typography>
              {item.quantity !== 1 && <Typography variant="body2" color="text.secondary">×{item.quantity}</Typography>}
            </Box>
          ))}
          {inventory.length === 0 && <Typography color="text.secondary">Empty inventory</Typography>}
        </Box>
      )}

      {/* Spells */}
      {tab === 3 && (
        <Box>
          {slots.length > 0 && (
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 2 }}>
              {slots.map(([level, slot]) => (
                <Paper key={level} sx={{ p: 1, minWidth: 72, textAlign: 'center' }}>
                  <Typography variant="caption" color="text.secondary" display="block">Level {level}</Typography>
                  <Typography variant="body1" fontWeight={700}>{slot.remaining}/{slot.total}</Typography>
                  <Typography variant="caption" color="text.secondary">slots</Typography>
                </Paper>
              ))}
            </Box>
          )}
          {Object.entries(spells).map(([level, spellList]) => (
            <Box key={level} sx={{ mb: 2 }}>
              <Typography variant="subtitle2" fontWeight={600}>Level {level}</Typography>
              <Divider sx={{ mb: 1 }} />
              {(spellList as string[])?.map((s, i) => (
                <Typography key={i} variant="body2" sx={{ py: 0.25 }}>• {typeof s === 'string' ? s : JSON.stringify(s)}</Typography>
              ))}
            </Box>
          ))}
          {Object.keys(spells).length === 0 && slots.length === 0 && <Typography color="text.secondary">No spells</Typography>}
        </Box>
      )}

      {/* Background */}
      {tab === 4 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField label="Background Description" value={character.background ?? ''}
            onChange={e => setCharacter(c => c ? { ...c, background: e.target.value } : c)}
            fullWidth multiline rows={2} />
          <TextField label="Background Skills" value={character.backgroundSkills ?? ''}
            onChange={e => setCharacter(c => c ? { ...c, backgroundSkills: e.target.value } : c)}
            fullWidth />
          <TextField label="Background Features" value={character.backgroundFeatures ?? ''}
            onChange={e => setCharacter(c => c ? { ...c, backgroundFeatures: e.target.value } : c)}
            fullWidth multiline rows={2} />
          <TextField label="Backstory" value={character.backstory ?? ''}
            onChange={e => setCharacter(c => c ? { ...c, backstory: e.target.value } : c)}
            fullWidth multiline rows={4} />
          <Button variant="contained" onClick={save} disabled={saving} sx={{ alignSelf: 'flex-start' }}>
            Save Background
          </Button>
        </Box>
      )}

      {/* Features */}
      {tab === 5 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Abilities that let you reroll. The GM offers these automatically when their trigger
            fires — you never have to ask. Limited uses come back on a rest. Racial abilities
            come with the character, and class abilities arrive with the level that grants them.
          </Typography>

          {optionsError && <Alert severity="warning">Could not load the ability list: {optionsError}</Alert>}

          {features.length === 0 && <Typography color="text.secondary">No abilities recorded.</Typography>}

          {features.map(f => {
            const def = options?.features.find(d => d.id === f.id)
            return (
              <Paper key={f.id} sx={{ p: 1.5, display: 'flex', alignItems: 'center', gap: 1 }}>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="subtitle2" fontWeight={600}>{f.name}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {def?.description ?? 'Not in the rules catalogue — the GM will not offer this automatically.'}
                  </Typography>
                </Box>
                <Chip
                  size="small"
                  label={f.usesRemaining == null ? 'Unlimited' : `${f.usesRemaining} left`}
                  color={f.usesRemaining === 0 ? 'default' : 'primary'}
                />
              </Paper>
            )
          })}
        </Box>
      )}

      {/* Custom */}
      {tab === 6 && (
        <Box>
          {/* The draft is kept as text: parsing on every keystroke used to swallow the
              half-typed JSON silently, so the box snapped back and edits were lost. */}
          <TextField
            label="Custom Fields (JSON)"
            value={customText ?? JSON.stringify(character.customFields ?? {}, null, 2)}
            onChange={e => {
              setCustomText(e.target.value)
              try {
                const parsed = JSON.parse(e.target.value) as Record<string, unknown>
                setCharacter(c => c ? { ...c, customFields: parsed } : c)
                setCustomError(null)
              } catch (err) {
                setCustomError((err as Error).message)
              }
            }}
            error={customError !== null}
            helperText={customError ?? ' '}
            fullWidth multiline rows={10} sx={{ fontFamily: 'monospace' }}
          />
          <Button variant="contained" onClick={save} disabled={saving || customError !== null} sx={{ mt: 1 }}>Save</Button>
        </Box>
      )}
    </Box>
  )
}
