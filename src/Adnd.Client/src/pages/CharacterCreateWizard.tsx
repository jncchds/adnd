import { useState, useEffect } from 'react';
import { Box, Typography, Paper, Button, Dialog, DialogTitle, DialogContent, DialogActions, Stepper, Step, StepLabel, StepContent, TextField, Chip, Card, CardContent, Alert, MenuItem, Select, Grid } from '@mui/material';
import { ArrowForward as NextIcon, ArrowBack as BackIcon, Save as SaveIcon } from '@mui/icons-material';
import { BACKGROUND_TEMPLATES, CLASS_TEMPLATES, STANDARD_ARRAYS, RACES } from '../components/character/CharacterTemplates';

interface CharacterCreateWizardProps {
  open: boolean;
  onClose: () => void;
  onFinish: (data: any) => void;
}

export default function CharacterCreateWizard({ open, onClose, onFinish }: CharacterCreateWizardProps) {
  const [step, setStep] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [classId, setClassId] = useState('');
  const [level, setLevel] = useState(1);
  const [systemId, setSystemId] = useState('dnd5e');
  const [attributeMethod, setAttributeMethod] = useState('template');
  const [attributes, setAttributes] = useState<Record<string, number>>({});
  const [standardArrayIndex, setStandardArrayIndex] = useState(0);
  const [standardArrayOrder, setStandardArrayOrder] = useState<string[]>([]);
  const [selectedBackground, setSelectedBackground] = useState('');
  const [selectedRace, setSelectedRace] = useState('human');
  const [_startingEquipment, _setStartingEquipment] = useState<{ name: string; type: string; quantity: number }[]>([]);
  const [extraGold, setExtraGold] = useState(0);

  useEffect(() => {
    if (open) {
      setStep(0); setName(''); setClassId(''); setLevel(1); setSystemId('dnd5e');
      setAttributeMethod('template'); setAttributes({}); setStandardArrayIndex(0);
      setStandardArrayOrder([]); setSelectedBackground(''); setSelectedRace('human');
      _setStartingEquipment([]); setExtraGold(0); setError(null); setSuccess(null);
    }
  }, [open]);

  const selectedTemplate = CLASS_TEMPLATES.find((t: any) => t.id === classId);
  const selectedRaceData = RACES.find((r: any) => r.id === selectedRace);

  useEffect(() => {
    if (selectedTemplate && attributeMethod === 'template') {
      setAttributes({ ...selectedTemplate.defaultAttributes });
    }
  }, [selectedTemplate, attributeMethod]);

  useEffect(() => {
    if (attributeMethod === 'standard' && STANDARD_ARRAYS[standardArrayIndex]) {
      const arr = STANDARD_ARRAYS[standardArrayIndex];
      const newAttrs: Record<string, number> = {};
      ['STR', 'DEX', 'CON', 'INT', 'WIS', 'CHA'].forEach((attr, i) => {
        newAttrs[attr] = (standardArrayOrder[i] !== undefined ? arr[Number(standardArrayOrder[i])] : arr[i]) || arr[i];
      });
      setAttributes(newAttrs);
    }
  }, [standardArrayIndex, standardArrayOrder, attributeMethod]);

  const handleNext = () => {
    if (step === 0 && !name.trim()) { setError('Character name is required'); return; }
    if (step === 1 && !classId) { setError('Please select a class'); return; }
    setStep(s => s + 1);
    setError(null);
  };

  const handleBack = () => setStep(s => s - 1);

  const handleFinish = () => {
    if (!name.trim()) { setError('Character name is required'); return; }
    if (!classId) { setError('Please select a class'); return; }
    const bg = BACKGROUND_TEMPLATES.find((b: any) => b.id === selectedBackground);
    onFinish({
      name, classId, level, systemId, attributes, selectedRace,
      background: selectedBackground, backgroundSkills: bg?.skillBonuses || [],
      backgroundLanguages: bg?.languages || [],
      startingEquipment: bg?.startingEquipment || [], extraGold,
    });
    onClose();
  };

  const handleSaveTemplate = () => {
    setSuccess('Character saved as template!');
    setTimeout(() => setSuccess(null), 2000);
  };

  const steps = ['Name & Class', 'Attributes', 'Race', 'Background', 'Equipment'];

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Create Character</DialogTitle>
      <DialogContent sx={{ mt: 1 }}>
        {error && <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>{error}</Alert>}
        {success && <Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2 }}>{success}</Alert>}
        <Stepper activeStep={step} orientation="vertical" sx={{ mb: 2 }}>
          {steps.map((label, i) => (
            <Step key={i}>
              <StepLabel>{label}</StepLabel>
              <StepContent>
                {i === 0 && (
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <TextField fullWidth label="Character Name" value={name} onChange={e => setName(e.target.value)} placeholder="Enter your character's name" autoFocus />
                    <TextField fullWidth select label="RPG System" value={systemId} onChange={e => setSystemId(e.target.value)}>
                      <MenuItem value="dnd5e">D&D 5th Edition</MenuItem>
                      <MenuItem value="pf2e">Pathfinder 2nd Edition</MenuItem>
                      <MenuItem value="coc7e">Call of Cthulhu 7th Edition</MenuItem>
                    </TextField>
                    <Typography variant="subtitle2" color="text.secondary">Select a class:</Typography>
                    <Grid container spacing={1}>
                      {CLASS_TEMPLATES.map((cls: any) => (
                        <Grid size={{ xs: 12, sm: 6, lg: 4 }} key={cls.id}>
                          <Card variant="outlined" onClick={() => setClassId(cls.id)} sx={{ cursor: 'pointer', bgcolor: classId === cls.id ? 'primary.light' : 'inherit', borderColor: classId === cls.id ? 'primary.main' : 'divider', transition: 'all 0.2s', '&:hover': { borderColor: 'primary.main' } }}>
                            <CardContent sx={{ p: 1.5 }}>
                              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                                <Typography variant="subtitle1" sx={{ fontWeight: 'bold' }}>{cls.name}</Typography>
                                <Chip label={`HD ${cls.hitDie}`} size="small" color="default" variant="outlined" />
                              </Box>
                              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>{cls.description}</Typography>
                              <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                                {cls.primaryAttributes.map((attr: string) => (<Chip key={attr} label={attr} size="small" color="primary" variant="filled" sx={{ fontSize: 10, height: 18 }} />))}
                              </Box>
                            </CardContent>
                          </Card>
                        </Grid>
                      ))}
                    </Grid>
                  </Box>
                )}
                {i === 1 && (
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Select fullWidth value={attributeMethod} onChange={e => setAttributeMethod(e.target.value)}>
                      <MenuItem value="template">Class Template</MenuItem>
                      <MenuItem value="standard">Standard Array</MenuItem>
                      <MenuItem value="pointbuy">Point Buy</MenuItem>
                      <MenuItem value="roll">Roll 4d6 drop lowest</MenuItem>
                    </Select>
                    {attributeMethod === 'standard' && (
                      <Box>
                        <Typography variant="subtitle2">Choose an array:</Typography>
                        <Select fullWidth value={standardArrayIndex} onChange={e => setStandardArrayIndex(Number(e.target.value))}>
                          {STANDARD_ARRAYS.map((arr: number[], i: number) => (
                            <MenuItem key={i} value={i}>{arr.join(', ')}</MenuItem>
                          ))}
                        </Select>
                      </Box>
                    )}
                    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 1 }}>
                      {Object.entries(attributes).map(([attr, val]) => (
                        <Paper key={attr} sx={{ p: 1, textAlign: 'center' }}>
                          <Typography variant="caption" color="text.secondary">{attr}</Typography>
                          <Typography variant="h6">{val}</Typography>
                          <Typography variant="caption" color="text.secondary">+{Math.floor((val - 10) / 2)}</Typography>
                        </Paper>
                      ))}
                    </Box>
                  </Box>
                )}
                {i === 2 && (
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Typography variant="subtitle2">Select a race:</Typography>
                    <Grid container spacing={1}>
                      {RACES.map((race: any) => (
                        <Grid size={{ xs: 12, sm: 6 }} key={race.id}>
                          <Card variant="outlined" onClick={() => setSelectedRace(race.id)} sx={{ cursor: 'pointer', bgcolor: selectedRace === race.id ? 'primary.light' : 'inherit', borderColor: selectedRace === race.id ? 'primary.main' : 'divider' }}>
                            <CardContent sx={{ p: 1.5 }}>
                              <Typography variant="subtitle1">{race.name}</Typography>
                              <Typography variant="caption" color="text.secondary">Speed: {race.speed} ft</Typography>
                              <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mt: 0.5 }}>
                                {race.traits.map((t: string) => (<Chip key={t} label={t} size="small" sx={{ fontSize: 9 }} />))}
                              </Box>
                            </CardContent>
                          </Card>
                        </Grid>
                      ))}
                    </Grid>
                    {selectedRaceData && (
                      <Paper sx={{ p: 1 }}>
                        <Typography variant="subtitle2">Ability Bonuses:</Typography>
                        <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                          {Object.entries(selectedRaceData.abilityScoreBonuses).map(([attr, bonus]) => (
                            <Chip key={attr} label={`+${bonus} ${attr}`} size="small" color="primary" variant="outlined" />
                          ))}
                        </Box>
                      </Paper>
                    )}
                  </Box>
                )}
                {i === 3 && (
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Typography variant="subtitle2">Select a background:</Typography>
                    <Grid container spacing={1}>
                      {BACKGROUND_TEMPLATES.map((bg: any) => (
                        <Grid size={{ xs: 12, sm: 6 }} key={bg.id}>
                          <Card variant="outlined" onClick={() => setSelectedBackground(bg.id)} sx={{ cursor: 'pointer', bgcolor: selectedBackground === bg.id ? 'primary.light' : 'inherit', borderColor: selectedBackground === bg.id ? 'primary.main' : 'divider' }}>
                            <CardContent sx={{ p: 1.5 }}>
                              <Typography variant="subtitle1">{bg.name}</Typography>
                              <Typography variant="caption" color="text.secondary">{bg.description}</Typography>
                              <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mt: 0.5 }}>
                                <Chip label={`Skills: ${bg.skillBonuses.join(', ')}`} size="small" sx={{ fontSize: 9 }} />
                                <Chip label={`Feature: ${bg.feature}`} size="small" sx={{ fontSize: 9 }} />
                              </Box>
                            </CardContent>
                          </Card>
                        </Grid>
                      ))}
                    </Grid>
                  </Box>
                )}
                {i === 4 && selectedTemplate && (
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Typography variant="subtitle2">Starting Equipment (from {selectedTemplate.name} class):</Typography>
                    {selectedTemplate.defaultAttributes && (
                      <Paper sx={{ p: 1 }}>
                        <Typography variant="body2">Gold: {Math.floor((selectedTemplate.defaultAttributes.CHA || 10) * 5) + extraGold} gp</Typography>
                      </Paper>
                    )}
                    <Typography variant="subtitle2">Languages:</Typography>
                    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                      <Chip label="Common" size="small" />
                      {selectedRaceData?.traits.includes('Extra Language') && <Chip label="Extra Language" size="small" color="primary" />}
                    </Box>
                  </Box>
                )}
                <Box sx={{ mt: 2, display: 'flex', justifyContent: 'space-between' }}>
                  <Button disabled={step === 0} onClick={handleBack} startIcon={<BackIcon />}>Back</Button>
                  <Box sx={{ display: 'flex', gap: 1 }}>
                    {step === steps.length - 1 ? (
                      <Button variant="contained" onClick={handleFinish} startIcon={<SaveIcon />}>Finish</Button>
                    ) : (
                      <Button variant="contained" onClick={handleNext} endIcon={<NextIcon />}>Next</Button>
                    )}
                    <Button variant="outlined" onClick={handleSaveTemplate}>Save as Template</Button>
                  </Box>
                </Box>
              </StepContent>
            </Step>
          ))}
        </Stepper>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
      </DialogActions>
    </Dialog>
  );
}
