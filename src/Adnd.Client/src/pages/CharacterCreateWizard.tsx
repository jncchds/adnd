import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
  Box, Typography, Button, Stepper, Step, StepLabel,
  TextField, Grid, CircularProgress, Alert, Card, CardActionArea, CardContent,
  MenuItem,
} from '@mui/material'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import CasinoIcon from '@mui/icons-material/Casino'
import { api } from '../api/client'
import type { CharacterOptions } from '../types'

const BACKGROUNDS = [
  { id: 'acolyte', label: 'Acolyte', desc: 'Temple servant. Skills: Insight, Religion. Feature: Shelter of the Faithful.' },
  { id: 'criminal', label: 'Criminal', desc: 'Life of crime. Skills: Deception, Stealth. Feature: Criminal Contact.' },
  { id: 'soldier', label: 'Soldier', desc: 'Military veteran. Skills: Athletics, Intimidation. Feature: Military Rank.' },
  { id: 'sage', label: 'Sage', desc: 'Scholar and researcher. Skills: Arcana, History. Feature: Researcher.' },
  { id: 'gladiator', label: 'Gladiator', desc: 'Arena fighter. Skills: Athletics, Performance. Feature: By Popular Demand.' },
  { id: 'folkhero', label: 'Folk Hero', desc: 'Humble origin, heroic destiny. Skills: Animal Handling, Survival. Feature: Rustic Hospitality.' },
  { id: 'urchin', label: 'Urchin', desc: 'City streets survivor. Skills: Sleight of Hand, Stealth. Feature: City Secrets.' },
  { id: 'noble', label: 'Noble', desc: 'Aristocratic blood. Skills: History, Persuasion. Feature: Position of Privilege.' },
]

const ATTRS = ['STR', 'DEX', 'CON', 'INT', 'WIS', 'CHA']
const STANDARD_ARRAY = [15, 14, 13, 12, 10, 8]

// Name and backstory come first because everything after them can be derived from them —
// that is what the suggest button on step 0 does.
const STEPS = ['Name & Backstory', 'Race, Class & Background', 'Attributes']

function isStandardArray(attrs: Record<string, number>) {
  const values = ATTRS.map(a => attrs[a]).sort((a, b) => a - b)
  const expected = [...STANDARD_ARRAY].sort((a, b) => a - b)
  return values.length === expected.length && values.every((v, i) => v === expected[i])
}

export default function CharacterCreateWizard() {
  const navigate = useNavigate()
  // Routed as /game/:id/character/new — the game is in the path, not a query string, so
  // the wizard cannot be opened without one and the sidebar shows the game's navigation.
  const { id: gameId = '' } = useParams<{ id: string }>()
  const [step, setStep] = useState(0)
  const [background, setBackground] = useState('')
  const [name, setName] = useState('')
  const [characterClass, setCharacterClass] = useState('Fighter')
  const [race, setRace] = useState('')
  const [options, setOptions] = useState<CharacterOptions | null>(null)
  const [optionsError, setOptionsError] = useState<string | null>(null)
  const [attrs, setAttrs] = useState<Record<string, number>>(
    Object.fromEntries(ATTRS.map((a, i) => [a, STANDARD_ARRAY[i]])),
  )
  const [backstory, setBackstory] = useState('')
  const [creating, setCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [suggesting, setSuggesting] = useState(false)
  const [suggestError, setSuggestError] = useState<string | null>(null)
  const [suggested, setSuggested] = useState(false)

  // Served rather than hardcoded so the list and the abilities each race grants cannot
  // disagree with the server's catalogue.
  useEffect(() => {
    api.characters.options()
      .then(o => { setOptions(o); setOptionsError(null) })
      .catch(e => setOptionsError((e as Error).message))
  }, [])

  // What picking this race and class will actually give the character. Worth showing: the
  // reroll prompt appearing mid-game is otherwise unexplained.
  const grantedFeatures = (options?.features ?? []).filter(f =>
    (f.grantedByRace != null && f.grantedByRace.toLowerCase() === race.trim().toLowerCase()) ||
    (f.grantedByClass != null && f.grantedByClass.toLowerCase() === characterClass.trim().toLowerCase() && f.grantedAtLevel <= 1))

  const fromScratch = !name.trim() && !backstory.trim()

  /**
   * Fills in everything the player hasn't written, from the campaign premise and the
   * narration so far. With both fields empty it invents the name and backstory too — the
   * server treats anything already typed as fixed, so this never overwrites the player.
   * The results land in the ordinary wizard state and stay editable.
   */
  const handleSuggest = async () => {
    setSuggesting(true)
    setSuggestError(null)
    setSuggested(false)
    try {
      const concept = await api.characters.suggest({
        gameId,
        name: name.trim() || undefined,
        backstory: backstory.trim() || undefined,
      })
      setName(concept.name)
      setBackstory(concept.backstory)
      if (concept.race) setRace(concept.race)
      if (concept.class) setCharacterClass(concept.class)
      if (concept.background) setBackground(concept.background)
      if (Object.keys(concept.attributes).length > 0) setAttrs(concept.attributes)
      setSuggested(true)
    } catch (e) { setSuggestError((e as Error).message) }
    finally { setSuggesting(false) }
  }

  const handleCreate = async () => {
    if (!name.trim()) { setError('Name is required'); return }
    setCreating(true)
    setError(null)
    try {
      await api.characters.create({
        gameId, name, class: characterClass,
        background, attributes: attrs,
        backstory: backstory || undefined,
        race: race || undefined,
      })
      // Back to the table, as before — the sheet is one click away in the game nav now.
      navigate(`/game/${gameId}`)
    } catch (e) { setError((e as Error).message) }
    finally { setCreating(false) }
  }

  return (
    <Box sx={{ p: 3, maxWidth: 800, mx: 'auto' }}>
      <Typography variant="h5" fontWeight={700} sx={{ mb: 3 }}>Create Character</Typography>
      <Stepper activeStep={step} sx={{ mb: 4 }}>
        {STEPS.map(s => <Step key={s}><StepLabel>{s}</StepLabel></Step>)}
      </Stepper>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {step === 0 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField label="Character Name" value={name} onChange={e => setName(e.target.value)} fullWidth />
          <TextField
            label="Character Backstory" multiline rows={8}
            value={backstory} onChange={e => setBackstory(e.target.value)} fullWidth
            placeholder="Describe your character's history, motivations, and personality..."
            helperText="This will be shared with the GM to personalize your story"
          />

          <Box>
            <Button
              variant="outlined"
              onClick={handleSuggest}
              disabled={suggesting}
              startIcon={suggesting
                ? <CircularProgress size={16} />
                : fromScratch ? <CasinoIcon /> : <AutoAwesomeIcon />}
            >
              {suggesting
                ? 'Asking the GM…'
                : fromScratch
                  ? 'Roll me a character'
                  : 'Fill in the rest from this'}
            </Button>
            <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 1 }}>
              {fromScratch
                ? 'Invents a whole character that fits this campaign and what has already happened in it. You can still change everything.'
                : 'Picks race, class, background and attributes to match what you wrote — and keeps your name and backstory as they are.'}
            </Typography>
          </Box>

          {suggestError && <Alert severity="error" onClose={() => setSuggestError(null)}>{suggestError}</Alert>}
          {suggested && (
            <Alert severity="success" onClose={() => setSuggested(false)}>
              Filled in: {race || '—'} {characterClass}, {BACKGROUNDS.find(b => b.id === background)?.label ?? 'no background'}.
              Step through the rest to review or change it.
            </Alert>
          )}
        </Box>
      )}

      {step === 1 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            select={options != null}
            label="Race"
            value={race}
            onChange={e => setRace(e.target.value)}
            fullWidth
            helperText={optionsError
              ? `Could not load the race list (${optionsError}) — type one instead.`
              : 'Some races grant abilities the GM will offer you automatically, such as Halfling Luck.'}
          >
            {(options?.races ?? []).map(r => <MenuItem key={r} value={r}>{r}</MenuItem>)}
          </TextField>
          <TextField label="Class" value={characterClass} onChange={e => setCharacterClass(e.target.value)} fullWidth
            helperText="e.g. Fighter, Wizard, Rogue, Cleric" />
          {grantedFeatures.length > 0 && (
            <Alert severity="success">
              You will start with: {grantedFeatures.map(f => f.name).join(', ')}.
            </Alert>
          )}

          <Typography variant="h6" sx={{ mt: 1 }}>Background</Typography>
          <Grid container spacing={2}>
            {BACKGROUNDS.map(bg => (
              <Grid size={{ xs: 12, sm: 6 }} key={bg.id}>
                <Card sx={{ border: background === bg.id ? '2px solid' : '1px solid', borderColor: background === bg.id ? 'primary.main' : 'divider' }}>
                  <CardActionArea onClick={() => setBackground(bg.id)}>
                    <CardContent>
                      <Typography variant="subtitle2" fontWeight={600}>{bg.label}</Typography>
                      <Typography variant="body2" color="text.secondary">{bg.desc}</Typography>
                    </CardContent>
                  </CardActionArea>
                </Card>
              </Grid>
            ))}
          </Grid>
        </Box>
      )}

      {step === 2 && (
        <Box>
          <Typography variant="h6" sx={{ mb: 2 }}>Set Attributes (standard array: 15,14,13,12,10,8)</Typography>
          <Grid container spacing={2}>
            {ATTRS.map(a => (
              <Grid size={{ xs: 6, sm: 4 }} key={a}>
                <TextField
                  label={a} type="number" value={attrs[a]}
                  onChange={e => setAttrs(prev => ({ ...prev, [a]: parseInt(e.target.value) || 10 }))}
                  inputProps={{ min: 1, max: 20 }} fullWidth
                />
              </Grid>
            ))}
          </Grid>
          {!isStandardArray(attrs) && (
            <Alert severity="warning" sx={{ mt: 2 }}>
              These values aren't the standard array (each of 15, 14, 13, 12, 10, 8 used once) —
              you can still continue, but double-check this is intentional.
            </Alert>
          )}
        </Box>
      )}

      <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 4 }}>
        <Button disabled={step === 0} onClick={() => setStep(s => s - 1)}>Back</Button>
        {step < STEPS.length - 1 ? (
          <Button variant="contained" onClick={() => setStep(s => s + 1)}
            disabled={(step === 0 && !name.trim()) || (step === 1 && !background)}>Next</Button>
        ) : (
          <Button variant="contained" onClick={handleCreate} disabled={creating}>
            {creating ? <CircularProgress size={18} /> : 'Create Character'}
          </Button>
        )}
      </Box>
    </Box>
  )
}
