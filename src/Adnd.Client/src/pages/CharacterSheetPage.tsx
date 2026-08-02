import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, Paper, Tabs, Tab, CircularProgress, Alert,
  TextField, Button, Chip, Divider, Grid, MenuItem, IconButton, Tooltip,
} from '@mui/material'
import { Delete as DeleteIcon } from '@mui/icons-material'
import { useCharacter } from '../api/hooks/useCharacters'
import { api } from '../api/client'
import type { CharacterFeature, CharacterOptions } from '../types'

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
  const { characterId } = useParams<{ id: string; characterId: string }>()
  const { character, loading, error, setCharacter } = useCharacter(characterId ?? null)
  const [tab, setTab] = useState(0)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [options, setOptions] = useState<CharacterOptions | null>(null)
  const [optionsError, setOptionsError] = useState<string | null>(null)
  const [featureToAdd, setFeatureToAdd] = useState('')

  useEffect(() => {
    api.characters.options()
      .then(o => { setOptions(o); setOptionsError(null) })
      .catch(e => setOptionsError((e as Error).message))
  }, [])

  const save = async () => {
    if (!characterId || !character) return
    setSaving(true)
    setSaveError(null)
    try {
      const updated = await api.characters.update(characterId, character)
      setCharacter(updated)
    } catch (e) { setSaveError((e as Error).message) }
    finally { setSaving(false) }
  }

  if (loading) return <Box sx={{ p: 3 }}><CircularProgress /></Box>
  if (error) return <Box sx={{ p: 3 }}><Alert severity="error">{error}</Alert></Box>
  if (!character) return <Box sx={{ p: 3 }}><Typography>Character not found</Typography></Box>

  const attrs = character.attributes ?? {}
  const skills = character.skills as Record<string, unknown> ?? {}
  const inventory = character.inventory ?? []
  const spells = character.spells as Record<string, unknown> ?? {}
  const features = character.features ?? []

  // Uses start full: the catalogue's maximum for a limited ability, null for an unlimited
  // one. Null here means unlimited everywhere else too, never "not yet known".
  const addFeature = () => {
    const def = options?.features.find(d => d.id === featureToAdd)
    if (!def) return
    const next: CharacterFeature = { id: def.id, name: def.name, usesRemaining: def.maxUses }
    setCharacter(c => c ? { ...c, features: [...(c.features ?? []), next] } : c)
    setFeatureToAdd('')
  }

  const removeFeature = (id: string) =>
    setCharacter(c => c ? { ...c, features: (c.features ?? []).filter(f => f.id !== id) } : c)

  return (
    <Box sx={{ p: 3, maxWidth: 900, mx: 'auto' }}>
      {/* Header */}
      <Box sx={{ mb: 3 }}>
        <Typography variant="h4" fontWeight={700}>{character.name}</Typography>
        <Typography variant="subtitle1" color="text.secondary">
          Level {character.level} {character.race ? `${character.race} ` : ''}{character.class} — HP: {character.currentHP}/{character.maxHP}
        </Typography>
      </Box>

      {saveError && <Alert severity="error" sx={{ mb: 2 }}>{saveError}</Alert>}

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
          {(inventory as string[]).map((item, i) => (
            <Box key={i} sx={{ py: 0.5, borderBottom: '1px solid', borderColor: 'divider' }}>
              <Typography variant="body2">{typeof item === 'string' ? item : JSON.stringify(item)}</Typography>
            </Box>
          ))}
          {inventory.length === 0 && <Typography color="text.secondary">Empty inventory</Typography>}
        </Box>
      )}

      {/* Spells */}
      {tab === 3 && (
        <Box>
          {Object.entries(spells).map(([level, spellList]) => (
            <Box key={level} sx={{ mb: 2 }}>
              <Typography variant="subtitle2" fontWeight={600}>Level {level}</Typography>
              <Divider sx={{ mb: 1 }} />
              {(spellList as string[])?.map((s, i) => (
                <Typography key={i} variant="body2" sx={{ py: 0.25 }}>• {typeof s === 'string' ? s : JSON.stringify(s)}</Typography>
              ))}
            </Box>
          ))}
          {Object.keys(spells).length === 0 && <Typography color="text.secondary">No spells</Typography>}
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
            fires — you never have to ask. Limited uses come back on a rest.
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
                <Tooltip title="Remove">
                  <IconButton size="small" onClick={() => removeFeature(f.id)}><DeleteIcon fontSize="small" /></IconButton>
                </Tooltip>
              </Paper>
            )
          })}

          <Divider />

          <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
            <TextField
              select
              size="small"
              label="Add an ability"
              value={featureToAdd}
              onChange={e => setFeatureToAdd(e.target.value)}
              sx={{ minWidth: 240 }}
              helperText="Racial and class abilities are granted at creation; add feats you take later."
            >
              {(options?.features ?? [])
                .filter(d => !features.some(f => f.id === d.id))
                .map(d => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}
            </TextField>
            <Button variant="outlined" disabled={!featureToAdd} onClick={addFeature} sx={{ mt: 0.5 }}>Add</Button>
          </Box>

          <Button variant="contained" onClick={save} disabled={saving} sx={{ alignSelf: 'flex-start' }}>
            Save Features
          </Button>
        </Box>
      )}

      {/* Custom */}
      {tab === 6 && (
        <Box>
          <TextField
            label="Custom Fields (JSON)"
            value={JSON.stringify(character.customFields ?? {}, null, 2)}
            onChange={e => {
              try { setCharacter(c => c ? { ...c, customFields: JSON.parse(e.target.value) as Record<string, unknown> } : c) }
              catch { /* invalid JSON, ignore */ }
            }}
            fullWidth multiline rows={10} sx={{ fontFamily: 'monospace' }}
          />
          <Button variant="contained" onClick={save} disabled={saving} sx={{ mt: 1 }}>Save</Button>
        </Box>
      )}
    </Box>
  )
}
