import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  Box, Typography, Button, Stepper, Step, StepLabel,
  TextField, Grid, CircularProgress, Alert, Card, CardActionArea, CardContent,
} from '@mui/material'
import { api } from '../api/client'

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
const STEPS = ['Background', 'Name & Class', 'Attributes', 'Backstory']

export default function CharacterCreateWizard() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const gameId = params.get('gameId') ?? ''
  const [step, setStep] = useState(0)
  const [background, setBackground] = useState('')
  const [name, setName] = useState('')
  const [characterClass, setCharacterClass] = useState('Fighter')
  const [attrs, setAttrs] = useState<Record<string, number>>({ STR: 10, DEX: 10, CON: 10, INT: 10, WIS: 10, CHA: 10 })
  const [backstory, setBackstory] = useState('')
  const [creating, setCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleCreate = async () => {
    if (!name.trim()) { setError('Name is required'); return }
    setCreating(true)
    setError(null)
    try {
      const char = await api.characters.create({
        gameId, name, class: characterClass,
        background, attributes: attrs,
        backstory: backstory || undefined,
      })
      navigate(`/character/${char.id}`)
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
        <Box>
          <Typography variant="h6" sx={{ mb: 2 }}>Choose your Background</Typography>
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

      {step === 1 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField label="Character Name" value={name} onChange={e => setName(e.target.value)} fullWidth />
          <TextField label="Class" value={characterClass} onChange={e => setCharacterClass(e.target.value)} fullWidth
            helperText="e.g. Fighter, Wizard, Rogue, Cleric" />
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
        </Box>
      )}

      {step === 3 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            label="Character Backstory" multiline rows={6}
            value={backstory} onChange={e => setBackstory(e.target.value)} fullWidth
            placeholder="Describe your character's history, motivations, and personality..."
            helperText="This will be shared with the GM to personalize your story"
          />
        </Box>
      )}

      <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 4 }}>
        <Button disabled={step === 0} onClick={() => setStep(s => s - 1)}>Back</Button>
        {step < STEPS.length - 1 ? (
          <Button variant="contained" onClick={() => setStep(s => s + 1)}
            disabled={step === 0 && !background}>Next</Button>
        ) : (
          <Button variant="contained" onClick={handleCreate} disabled={creating}>
            {creating ? <CircularProgress size={18} /> : 'Create Character'}
          </Button>
        )}
      </Box>
    </Box>
  )
}
