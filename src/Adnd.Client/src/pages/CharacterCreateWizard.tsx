import { useState, useEffect } from 'react';
import { useGameHub } from '../api/hubHook';
import {
  Box, Typography, Paper, Button, Step, StepLabel, StepContent,
  Stepper, TextField, MenuItem, Select,
  Grid, Chip, Alert, IconButton,
  List, ListItem, ListItemText, ListItemSecondaryAction,
  Dialog, DialogTitle, DialogContent, DialogActions,
  RadioGroup, FormControlLabel, Radio,
  Card, CardContent,
} from '@mui/material';
import {
  Delete as DeleteIcon,
  ArrowForward as NextIcon,
  ArrowBack as BackIcon, People as PeopleIcon,
  Save as SaveIcon,
} from '@mui/icons-material';

// ==================== Class Templates ====================

interface ClassTemplate {
  id: string;
  name: string;
  description: string;
  hitDie: number;
  primaryAttributes: string[];
  skills: string[];
  proficiencies: string[];
  startingEquipment: { name: string; type: string; quantity: number }[];
  defaultAttributes: Record<string, number>;
}

const CLASS_TEMPLATES: ClassTemplate[] = [
  {
    id: 'fighter',
    name: 'Fighter',
    description: 'A master of martial combat, skilled with a variety of weapons and armor.',
    hitDie: 10,
    primaryAttributes: ['STR', 'CON'],
    skills: ['Acrobatics', 'Animal Handling', 'Athletics', 'Insight', 'Intimidation', 'Perception', 'Survival'],
    proficiencies: ['All armor', 'Shields', 'Simple weapons', 'Martial weapons'],
    defaultAttributes: { STR: 15, DEX: 13, CON: 14, INT: 10, WIS: 10, CHA: 8 },
    startingEquipment: [
      { name: 'Longsword', type: 'Weapon', quantity: 1 },
      { name: 'Shield', type: 'Armor', quantity: 1 },
      { name: 'Light Crossbow', type: 'Weapon', quantity: 1 },
      { name: 'Crossbow bolts (20)', type: 'Ammunition', quantity: 20 },
      { name: 'Dungeoneer\'s pack', type: 'Equipment', quantity: 1 },
    ],
  },
  {
    id: 'wizard',
    name: 'Wizard',
    description: 'A scholarly magic-user capable of manipulating the structures of reality.',
    hitDie: 6,
    primaryAttributes: ['INT'],
    skills: ['Arcana', 'History', 'Insight', 'Investigation', 'Medicine', 'Religion'],
    proficiencies: [' Daggers', 'Darts', 'Slings', 'Quarterstaffs', 'Light crossbows'],
    defaultAttributes: { STR: 8, DEX: 14, CON: 13, INT: 16, WIS: 12, CHA: 10 },
    startingEquipment: [
      { name: 'Quarterstaff', type: 'Weapon', quantity: 1 },
      { name: 'Magic spellbook', type: 'Equipment', quantity: 1 },
      { name: 'Elemental pouch', type: 'Equipment', quantity: 1 },
      { name: 'Scholar\'s pack', type: 'Equipment', quantity: 1 },
    ],
  },
  {
    id: 'rogue',
    name: 'Rogue',
    description: 'A scoundrel who uses stealth and trickery to overcome obstacles and enemies.',
    hitDie: 8,
    primaryAttributes: ['DEX'],
    skills: ['Acrobatics', 'Athletics', 'Deception', 'Insight', 'Intimidation', 'Investigation', 'Perception', 'Performance', 'Persuasion', 'Sleight of Hand', 'Stealth'],
    proficiencies: ['Light armor', 'Simple weapons', 'Hand crossbows', 'Rapiers', 'Shortswords', 'Thieves\' tools'],
    defaultAttributes: { STR: 10, DEX: 16, CON: 13, INT: 14, WIS: 12, CHA: 8 },
    startingEquipment: [
      { name: 'Rapier', type: 'Weapon', quantity: 1 },
      { name: 'Shortbow', type: 'Weapon', quantity: 1 },
      { name: 'Arrows (20)', type: 'Ammunition', quantity: 20 },
      { name: 'Burglar\'s pack', type: 'Equipment', quantity: 1 },
      { name: 'Leather armor', type: 'Armor', quantity: 1 },
      { name: 'Thieves\' tools', type: 'Tool', quantity: 1 },
    ],
  },
  {
    id: 'cleric',
    name: 'Cleric',
    description: 'A priestly champion who wields divine magic in service of a higher power.',
    hitDie: 8,
    primaryAttributes: ['WIS'],
    skills: ['History', 'Insight', 'Medicine', 'Persuasion', 'Religion'],
    proficiencies: ['Light armor', 'Medium armor', 'Shields', 'Simple weapons'],
    defaultAttributes: { STR: 13, DEX: 10, CON: 14, INT: 10, WIS: 16, CHA: 12 },
    startingEquipment: [
      { name: 'Mace', type: 'Weapon', quantity: 1 },
      { name: 'Scale mail', type: 'Armor', quantity: 1 },
      { name: 'Shield', type: 'Armor', quantity: 1 },
      { name: 'Holy symbol', type: 'Tool', quantity: 1 },
      { name: 'Priest\'s pack', type: 'Equipment', quantity: 1 },
    ],
  },
  {
    id: 'ranger',
    name: 'Ranger',
    description: 'A warrior who uses martial prowess and nature magic to combat threats on the frontier.',
    hitDie: 10,
    primaryAttributes: ['DEX', 'WIS'],
    skills: ['Animal Handling', 'Athletics', 'Insight', 'Investigation', 'Nature', 'Perception', 'Survival'],
    proficiencies: ['Light armor', 'Medium armor', 'Shields', 'Simple weapons', 'Martial weapons'],
    defaultAttributes: { STR: 12, DEX: 16, CON: 14, INT: 10, WIS: 14, CHA: 8 },
    startingEquipment: [
      { name: 'Longbow', type: 'Weapon', quantity: 1 },
      { name: 'Arrows (20)', type: 'Ammunition', quantity: 20 },
      { name: 'Two handaxes', type: 'Weapon', quantity: 2 },
      { name: 'Explorer\'s pack', type: 'Equipment', quantity: 1 },
      { name: 'Leather armor', type: 'Armor', quantity: 1 },
    ],
  },
  {
    id: 'barbarian',
    name: 'Barbarian',
    description: 'A fierce warrior who can charge into battle with reckless fury.',
    hitDie: 12,
    primaryAttributes: ['STR', 'CON'],
    skills: ['Animal Handling', 'Athletics', 'Intimidation', 'Nature', 'Perception', 'Survival'],
    proficiencies: ['Light armor', 'Medium armor', 'Shields', 'Simple weapons', 'Martial weapons'],
    defaultAttributes: { STR: 16, DEX: 12, CON: 16, INT: 8, WIS: 10, CHA: 8 },
    startingEquipment: [
      { name: 'Two handaxes', type: 'Weapon', quantity: 2 },
      { name: 'Javelin (4)', type: 'Weapon', quantity: 4 },
      { name: 'Explorer\'s pack', type: 'Equipment', quantity: 1 },
      { name: 'Shield', type: 'Armor', quantity: 1 },
    ],
  },
  {
    id: 'bard',
    name: 'Bard',
    description: 'A magical performer whose spells are drawn from the power of music.',
    hitDie: 8,
    primaryAttributes: ['CHA'],
    skills: ['Acrobatics', 'Animal Handling', 'Arcana', 'Athletics', 'Deception', 'History', 'Insight', 'Intimidation', 'Investigation', 'Medicine', 'Nature', 'Perception', 'Performance', 'Persuasion', 'Religion', 'Sleight of Hand', 'Stealth', 'Survival'],
    proficiencies: ['Light armor', 'Simple weapons', 'Hand crossbows', 'Longswords', 'Rapiers', 'Shortswords', 'Three musical instruments'],
    defaultAttributes: { STR: 10, DEX: 14, CON: 12, INT: 12, WIS: 10, CHA: 16 },
    startingEquipment: [
      { name: 'Rapier', type: 'Weapon', quantity: 1 },
      { name: 'Leather armor', type: 'Armor', quantity: 1 },
      { name: 'Diplomat\'s pack', type: 'Equipment', quantity: 1 },
      { name: 'Musical instrument', type: 'Tool', quantity: 1 },
      { name: 'Lucky charm', type: 'Equipment', quantity: 1 },
    ],
  },
  {
    id: 'druid',
    name: 'Druid',
    description: 'A priest of the Old Faith, wielding divine magic in service to the forces of nature.',
    hitDie: 8,
    primaryAttributes: ['WIS'],
    skills: ['Arcana', 'Animal Handling', 'Insight', 'Medicine', 'Nature', 'Perception', 'Religion', 'Survival'],
    proficiencies: ['Light armor', 'Medium armor', 'Shields', 'Clubs', 'Daggers', 'Darts', 'Javelins', 'Maces', 'Quarterstaffs', 'Scimitars', 'Sickles', 'Slings', 'Spears', 'Herbalism kit'],
    defaultAttributes: { STR: 10, DEX: 14, CON: 13, INT: 12, WIS: 16, CHA: 8 },
    startingEquipment: [
      { name: 'Wooden shield', type: 'Armor', quantity: 1 },
      { name: 'Scimitar', type: 'Weapon', quantity: 1 },
      { name: 'Herbalism kit', type: 'Tool', quantity: 1 },
      { name: 'Explorer\'s pack', type: 'Equipment', quantity: 1 },
    ],
  },
  {
    id: 'monk',
    name: 'Monk',
    description: 'A master of martial arts who can channel ki to bend the limits of human potential.',
    hitDie: 8,
    primaryAttributes: ['DEX', 'WIS'],
    skills: ['Acrobatics', 'Athletics', 'History', 'Insight', 'Religion', 'Stealth'],
    proficiencies: ['Simple weapons', 'Shortswords', 'Hands', 'Kama', 'Nunchaku', 'Quarterstaff', 'Sai'],
    defaultAttributes: { STR: 12, DEX: 16, CON: 13, INT: 10, WIS: 14, CHA: 10 },
    startingEquipment: [
      { name: 'Shortsword', type: 'Weapon', quantity: 1 },
      { name: 'Dungeoneer\'s pack', type: 'Equipment', quantity: 1 },
      { name: '10 darts', type: 'Weapon', quantity: 10 },
    ],
  },
  {
    id: 'paladin',
    name: 'Paladin',
    description: 'A holy warrior bound to a sacred oath, wielding a blend of martial and divine power.',
    hitDie: 10,
    primaryAttributes: ['STR', 'CHA'],
    skills: ['Athletics', 'Insight', 'Intimidation', 'Medicine', 'Persuasion', 'Religion'],
    proficiencies: ['All armor', 'Shields', 'Simple weapons', 'Martial weapons'],
    defaultAttributes: { STR: 15, DEX: 10, CON: 14, INT: 10, WIS: 10, CHA: 16 },
    startingEquipment: [
      { name: 'Longsword', type: 'Weapon', quantity: 1 },
      { name: 'Five javelins', type: 'Weapon', quantity: 5 },
      { name: 'Priest\'s pack', type: 'Equipment', quantity: 1 },
      { name: 'Chain mail', type: 'Armor', quantity: 1 },
      { name: 'Holy symbol', type: 'Tool', quantity: 1 },
    ],
  },
  {
    id: 'sorcerer',
    name: 'Sorcerer',
    description: 'A spellcaster who draws on inherent gifts from a magical bloodline.',
    hitDie: 6,
    primaryAttributes: ['CHA'],
    skills: ['Arcana', 'Deception', 'Insight', 'Intimidation', 'Persuasion', 'Religion'],
    proficiencies: ['Daggers', 'Darts', 'Slings', 'Quarterstaffs', 'Light crossbows'],
    defaultAttributes: { STR: 8, DEX: 14, CON: 14, INT: 12, WIS: 10, CHA: 16 },
    startingEquipment: [
      { name: 'Light crossbow', type: 'Weapon', quantity: 1 },
      { name: 'Arrows (20)', type: 'Ammunition', quantity: 20 },
      { name: 'Arcane focus', type: 'Tool', quantity: 1 },
      { name: 'Dungeoneer\'s pack', type: 'Equipment', quantity: 1 },
      { name: 'Two daggers', type: 'Weapon', quantity: 2 },
    ],
  },
  {
    id: 'warlock',
    name: 'Warlock',
    description: 'A wizard who has made a pact with a powerful being to gain magical abilities.',
    hitDie: 8,
    primaryAttributes: ['CHA'],
    skills: ['Arcana', 'Deception', 'History', 'Intimidation', 'Investigation', 'Nature', 'Religion'],
    proficiencies: ['Light armor', 'Simple weapons'],
    defaultAttributes: { STR: 10, DEX: 14, CON: 12, INT: 12, WIS: 10, CHA: 16 },
    startingEquipment: [
      { name: 'Light crossbow', type: 'Weapon', quantity: 1 },
      { name: 'Arrows (20)', type: 'Ammunition', quantity: 20 },
      { name: 'Scholar\'s pack', type: 'Equipment', quantity: 1 },
      { name: 'Leather armor', type: 'Armor', quantity: 1 },
      { name: 'Simple weapon', type: 'Weapon', quantity: 1 },
      { name: 'Arcane focus', type: 'Tool', quantity: 1 },
    ],
  },
];

// ==================== Standard Arrays ====================

const STANDARD_ARRAYS = [
  [15, 14, 13, 12, 10, 8],
  [15, 13, 12, 11, 10, 8],
  [14, 14, 12, 12, 10, 8],
  [15, 14, 10, 10, 9, 8],
  [13, 13, 12, 12, 11, 6],
  [14, 12, 12, 10, 10, 10],
];

const ATTR_NAMES = ['STR', 'DEX', 'CON', 'INT', 'WIS', 'CHA'];

// ==================== Component ====================

export default function CharacterCreateWizard({ open, onClose, gameId: _gameId }: { open: boolean; onClose: () => void; gameId: string }) {
  const { invoke } = useGameHub();
  const [step, setStep] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Character data
  const [name, setName] = useState('');
  const [classId, setClassId] = useState('');
  const [level, setLevel] = useState(1);
  const [systemId, setSystemId] = useState('dnd5e');
  const [attributeMethod, setAttributeMethod] = useState('template'); // 'template', 'standard', 'pointbuy', 'roll'
  const [attributes, setAttributes] = useState<Record<string, number>>({});
  const [standardArrayIndex, setStandardArrayIndex] = useState(0);
  const [standardArrayOrder, setStandardArrayOrder] = useState<string[]>([]);

  const [startingEquipment, setStartingEquipment] = useState<{ name: string; type: string; quantity: number }[]>([]);
  const [extraGold, setExtraGold] = useState(0);

  const selectedTemplate = CLASS_TEMPLATES.find(t => t.id === classId);

  // Reset when dialog opens
  useEffect(() => {
    if (open) {
      setStep(0);
      setName('');
      setClassId('');
      setLevel(1);
      setSystemId('dnd5e');
      setAttributeMethod('template');
      setAttributes({});
      setStandardArrayIndex(0);
      setStandardArrayOrder([]);
      // point buy starts at 27
      setStartingEquipment([]);
      setExtraGold(0);
      setError(null);
      setSuccess(null);
    }
  }, [open]);

  // Apply template attributes
  useEffect(() => {
    if (selectedTemplate && attributeMethod === 'template') {
      setAttributes({ ...selectedTemplate.defaultAttributes });
    }
  }, [selectedTemplate, attributeMethod]);

  // Apply standard array
  useEffect(() => {
    if (attributeMethod === 'standard' && STANDARD_ARRAYS[standardArrayIndex]) {
      const arr = STANDARD_ARRAYS[standardArrayIndex];
      const newAttrs: Record<string, number> = {};
      ATTR_NAMES.forEach((name, i) => {
        newAttrs[name] = standardArrayOrder[i] !== undefined ? parseInt(standardArrayOrder[i]) : arr[i];
      });
      setAttributes(newAttrs);
    }
  }, [attributeMethod, standardArrayIndex, standardArrayOrder]);

  const handleNext = () => {
    if (step === 0 && !name.trim()) { setError('Character name is required'); return; }
    if (step === 1 && !classId) { setError('Please select a class'); return; }
    setStep(s => s + 1);
    setError(null);
  };

  const handleBack = () => setStep(s => Math.max(0, s - 1));

  const handleFinish = async () => {
    if (!name.trim() || !classId) { setError('Name and class are required'); return; }
    setError(null);
    setSuccess(null);
    try {
      const characterData = {
        name,
        class: selectedTemplate?.name || classId,
        level,
        systemId,
        attributes: { ...attributes },
        skills: {},
        proficiencyBonus: 2,
        maxHP: selectedTemplate ? selectedTemplate.hitDie + Math.floor((attributes.CON - 10) / 2) : 10,
        currentHP: selectedTemplate ? selectedTemplate.hitDie + Math.floor((attributes.CON - 10) / 2) : 10,
        startingEquipment,
        gold: extraGold,
      };

      // Use agent framework to create character
      await invoke('CallAgent', 0, 5, 6, JSON.stringify(characterData));

      setSuccess(`Character '${name}' created successfully!`);
      setTimeout(() => {
        onClose();
      }, 1500);
    } catch (e: any) {
      setError(e.message || 'Failed to create character');
    }
  };

  // Point buy costs
  const getPointBuyCost = (value: number): number => {
    if (value <= 8) return 0;
    if (value === 9) return 1;
    if (value === 10) return 2;
    if (value === 11) return 3;
    if (value === 12) return 4;
    if (value === 13) return 5;
    if (value === 14) return 7;
    if (value === 15) return 9;
    return 0; // capped at 15
  };

  const getPointBuyTotal = (): number => {
    return ATTR_NAMES.reduce((sum, name) => sum + getPointBuyCost(attributes[name] || 8), 0);
  };

  const getModifier = (value: number): string => {
    const mod = Math.floor((value - 10) / 2);
    return mod >= 0 ? `+${mod}` : `${mod}`;
  };

  const stepLabels = ['Name & Class', 'Attributes', 'Equipment', 'Review'];

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <PeopleIcon /> Create New Character
        </Box>
      </DialogTitle>
      <DialogContent sx={{ mt: 1 }}>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {success && <Alert severity="success" sx={{ mb: 2 }}>{success}</Alert>}

        {/* Stepper */}
        <Stepper activeStep={step} orientation="vertical" sx={{ mb: 3 }}>
          {stepLabels.map((label, i) => (
            <Step key={i} completed={i < step}>
              <StepLabel
                onClick={() => i <= step && setStep(i)}
                sx={{ cursor: i <= step ? 'pointer' : 'default' }}
              >
                {label}
              </StepLabel>
              <StepContent>
                {i === step && (
                  <Box sx={{ mt: 2 }}>
                    {/* ===== Step 0: Name & Class ===== */}
                    {step === 0 && (
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        <TextField
                          fullWidth
                          label="Character Name"
                          value={name}
                          onChange={e => setName(e.target.value)}
                          placeholder="Enter your character's name"
                          autoFocus
                        />
                        <TextField
                          fullWidth
                          select
                          label="RPG System"
                          value={systemId}
                          onChange={e => setSystemId(e.target.value)}
                        >
                          <MenuItem value="dnd5e">D&D 5th Edition</MenuItem>
                          <MenuItem value="pf2e">Pathfinder 2nd Edition</MenuItem>
                          <MenuItem value="coc7e">Call of Cthulhu 7th Edition</MenuItem>
                        </TextField>
                        <Typography variant="subtitle2" color="text.secondary">Select a class:</Typography>
                        <Grid container spacing={1}>
                          {CLASS_TEMPLATES.map(cls => (
                            <Grid size={{ xs: 12, sm: 6, lg: 4 }} key={cls.id}>
                              <Card
                                variant="outlined"
                                onClick={() => setClassId(cls.id)}
                                sx={{
                                  cursor: 'pointer',
                                  bgcolor: classId === cls.id ? 'primary.light' : 'inherit',
                                  borderColor: classId === cls.id ? 'primary.main' : 'divider',
                                  transition: 'all 0.2s',
                                  '&:hover': { borderColor: 'primary.main' },
                                }}
                              >
                                <CardContent sx={{ p: 1.5 }}>
                                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                                    <Typography variant="subtitle1" sx={{ fontWeight: 'bold' }}>{cls.name}</Typography>
                                    <Chip label={`HD ${cls.hitDie}`} size="small" color="default" variant="outlined" />
                                  </Box>
                                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                                    {cls.description}
                                  </Typography>
                                  <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                                    {cls.primaryAttributes.map(attr => (
                                      <Chip key={attr} label={attr} size="small" color="primary" variant="filled" sx={{ fontSize: 10, height: 18 }} />
                                    ))}
                                  </Box>
                                </CardContent>
                              </Card>
                            </Grid>
                          ))}
                        </Grid>
                      </Box>
                    )}

                    {/* ===== Step 1: Attributes ===== */}
                    {step === 1 && (
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        <Typography variant="subtitle2">Choose an attribute generation method:</Typography>
                        <RadioGroup value={attributeMethod} onChange={e => setAttributeMethod(e.target.value)}>
                          <FormControlLabel value="template" control={<Radio />} label="Class Template (pre-set stats)" />
                          <FormControlLabel value="standard" control={<Radio />} label="Standard Array (15, 14, 13, 12, 10, 8)" />
                          <FormControlLabel value="pointbuy" control={<Radio />} label="Point Buy (27 points)" />
                          <FormControlLabel value="roll" control={<Radio />} label="Roll 4d6 drop lowest (×3)" />
                        </RadioGroup>

                        {/* Standard Array selector */}
                        {attributeMethod === 'standard' && (
                          <Box>
                            <Typography variant="subtitle2" gutterBottom>Choose an array and assign to attributes:</Typography>
                            <Grid container spacing={1} sx={{ mb: 2 }}>
                              {STANDARD_ARRAYS.map((arr, i) => (
                                <Grid size={{ xs: 4 }} key={i}>
                                  <Card
                                    variant="outlined"
                                    onClick={() => setStandardArrayIndex(i)}
                                    sx={{
                                      cursor: 'pointer',
                                      bgcolor: standardArrayIndex === i ? 'primary.light' : 'inherit',
                                      borderColor: standardArrayIndex === i ? 'primary.main' : 'divider',
                                    }}
                                  >
                                    <CardContent sx={{ p: 1, textAlign: 'center' }}>
                                      <Typography variant="body2">{arr.join(', ')}</Typography>
                                    </CardContent>
                                  </Card>
                                </Grid>
                              ))}
                            </Grid>
                            <Typography variant="subtitle2" gutterBottom>Assign values to attributes:</Typography>
                            <Grid container spacing={1}>
                              {ATTR_NAMES.map((attr, i) => (
                                <Grid size={{ xs: 2 }} key={attr}>
                                  <Select
                                    size="small"
                                    fullWidth
                                    value={String(ATTR_NAMES.indexOf(standardArrayOrder[i] || STANDARD_ARRAYS[standardArrayIndex][i].toString()))}
                                    onChange={e => {
                                      const idx = parseInt(e.target.value);
                                      const val = STANDARD_ARRAYS[standardArrayIndex][idx];
                                      const newOrder = [...standardArrayOrder];
                                      newOrder[i] = val.toString();
                                      setStandardArrayOrder(newOrder);
                                    }}
                                  >
                                    {STANDARD_ARRAYS[standardArrayIndex].map((val, vi) => (
                                      <MenuItem key={vi} value={vi}>{val}</MenuItem>
                                    ))}
                                  </Select>
                                </Grid>
                              ))}
                            </Grid>
                          </Box>
                        )}

                        {/* Point Buy */}
                        {attributeMethod === 'pointbuy' && (
                          <Box>
                            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                              <Typography variant="subtitle2">Points remaining:</Typography>
                              <Chip label={`${27 - getPointBuyTotal()} / 27`} color={getPointBuyTotal() > 27 ? 'error' : 'success'} size="small" />
                            </Box>
                            <Grid container spacing={1}>
                              {ATTR_NAMES.map(attr => {
                                const value = attributes[attr] || 8;
                                getPointBuyCost(value);
                                return (
                                  <Grid size={2} key={attr}>
                                    <Box sx={{ textAlign: 'center' }}>
                                      <Typography variant="caption" color="text.secondary">{attr}</Typography>
                                      <Box sx={{ display: 'flex', justifyContent: 'center', gap: 0.5, my: 0.5 }}>
                                        <IconButton size="small" disabled={value <= 8}
                                          onClick={() => setAttributes(prev => ({ ...prev, [attr]: Math.max(8, (prev[attr] || 8) - 1) }))}>
                                          -
                                        </IconButton>
                                        <Typography variant="h6">{value}</Typography>
                                        <IconButton size="small" disabled={value >= 15 || getPointBuyTotal() >= 27}
                                          onClick={() => setAttributes(prev => ({ ...prev, [attr]: Math.min(15, (prev[attr] || 8) + 1) }))}>
                                          +
                                        </IconButton>
                                      </Box>
                                      <Chip label={getModifier(value)} size="small" color={value >= 14 ? 'success' : value <= 8 ? 'error' : 'default'} />
                                    </Box>
                                  </Grid>
                                );
                              })}
                            </Grid>
                          </Box>
                        )}

                        {/* Roll */}
                        {attributeMethod === 'roll' && (
                          <Box sx={{ textAlign: 'center' }}>
                            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                              Roll 4d6, drop lowest. Repeat 6 times. Drag values to assign.
                            </Typography>
                            <Box sx={{ display: 'flex', gap: 1, justifyContent: 'center', flexWrap: 'wrap', mb: 2 }}>
                              {[0, 1, 2, 3, 4, 5].map(i => (
                                <Chip
                                  key={i}
                                  label={attributes[ATTR_NAMES[i]] || '—'}
                                  size="medium"
                                  color="primary"
                                  variant="filled"
                                  sx={{ fontSize: '1.1rem', height: 36, minWidth: 48 }}
                                />
                              ))}
                            </Box>
                            <Button variant="outlined" onClick={() => {
                              const newAttrs: Record<string, number> = {};
                              ATTR_NAMES.forEach(attr => {
                                // Simulate 4d6 drop lowest
                                const rolls = Array.from({ length: 4 }, () => Math.floor(Math.random() * 6) + 1);
                                rolls.sort((a, b) => a - b);
                                newAttrs[attr] = rolls.slice(1).reduce((s, v) => s + v, 0);
                              });
                              setAttributes(newAttrs);
                            }}>
                              🎲 Roll All
                            </Button>
                          </Box>
                        )}

                        {/* Attribute Summary */}
                        <Paper sx={{ p: 2, bgcolor: 'background.default' }}>
                          <Typography variant="subtitle2" gutterBottom>Attribute Summary:</Typography>
                          <Grid container spacing={1}>
                            {ATTR_NAMES.map(attr => (
                              <Grid size={{ xs: 2 }} key={attr}>
                                <Box sx={{ textAlign: 'center' }}>
                                  <Typography variant="caption" color="text.secondary">{attr}</Typography>
                                  <Typography variant="h6">{attributes[attr] || '—'}</Typography>
                                  <Chip label={getModifier(attributes[attr] || 10)} size="small" />
                                </Box>
                              </Grid>
                            ))}
                          </Grid>
                          {selectedTemplate && (
                            <Box sx={{ mt: 1 }}>
                              <Typography variant="body2">
                                Hit Die: <strong>{selectedTemplate.hitDie}</strong> ·
                                Max HP at Lv.1: <strong>{selectedTemplate.hitDie + Math.floor((attributes.CON - 10) / 2)}</strong> ·
                                Proficiency: <strong>+2</strong>
                              </Typography>
                            </Box>
                          )}
                        </Paper>
                      </Box>
                    )}

                    {/* ===== Step 2: Equipment ===== */}
                    {step === 2 && (
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        {selectedTemplate && (
                          <>
                            <Box>
                              <Typography variant="subtitle2" gutterBottom>Proficiencies:</Typography>
                              <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                                {selectedTemplate.proficiencies.map(p => (
                                  <Chip key={p} label={p} size="small" variant="outlined" color="primary" />
                                ))}
                              </Box>
                            </Box>
                            <Box>
                              <Typography variant="subtitle2" gutterBottom>Starting Equipment:</Typography>
                              <List dense>
                                {startingEquipment.length === 0 ? (
                                  <Typography variant="body2" color="text.secondary">
                                    No equipment selected. Choose below or use defaults.
                                  </Typography>
                                ) : (
                                  startingEquipment.map((item, i) => (
                                    <ListItem key={i} sx={{ px: 0 }}>
                                      <ListItemText
                                        primary={`${item.quantity > 1 ? `${item.quantity}x ` : ''}${item.name}`}
                                        secondary={item.type}
                                      />
                                      <ListItemSecondaryAction>
                                        <IconButton size="small" color="error" onClick={() => setStartingEquipment(prev => prev.filter((_, idx) => idx !== i))}>
                                          <DeleteIcon fontSize="small" />
                                        </IconButton>
                                      </ListItemSecondaryAction>
                                    </ListItem>
                                  ))
                                )}
                              </List>
                            </Box>
                            <Box>
                              <Typography variant="subtitle2" gutterBottom>Available Starting Equipment:</Typography>
                              <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                                {selectedTemplate.startingEquipment.map((item, i) => {
                                  const alreadyAdded = startingEquipment.some(e => e.name === item.name);
                                  return (
                                    <Chip
                                      key={i}
                                      label={`${item.quantity > 1 ? `${item.quantity}x ` : ''}${item.name}`}
                                      size="small"
                                      clickable
                                      disabled={alreadyAdded}
                                      onClick={() => setStartingEquipment(prev => [...prev, { ...item }])}
                                      color={alreadyAdded ? 'default' : 'primary'}
                                      variant={alreadyAdded ? 'outlined' : 'filled'}
                                    />
                                  );
                                })}
                              </Box>
                            </Box>
                          </>
                        )}
                        {!selectedTemplate && (
                          <Typography color="text.secondary">Select a class to see equipment options.</Typography>
                        )}
                        <Box>
                          <TextField
                            fullWidth
                            label="Extra Gold (CP)"
                            type="number"
                            value={extraGold}
                            onChange={e => setExtraGold(parseInt(e.target.value) || 0)}
                            inputProps={{ min: 0, max: 99999 }}
                          />
                        </Box>
                      </Box>
                    )}

                    {/* ===== Step 3: Review ===== */}
                    {step === 3 && selectedTemplate && (
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        <Paper sx={{ p: 2 }}>
                          <Typography variant="h6" gutterBottom>{name}</Typography>
                          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 1 }}>
                            <Chip label={selectedTemplate.name} color="primary" />
                            <Chip label={`Lv. ${level}`} />
                            <Chip label={systemId} />
                            <Chip label={`HP: ${selectedTemplate.hitDie + Math.floor((attributes.CON - 10) / 2)}`} color="error" />
                          </Box>
                        </Paper>

                        <Paper sx={{ p: 2 }}>
                          <Typography variant="subtitle2" gutterBottom>Attributes:</Typography>
                          <Grid container spacing={1}>
                            {ATTR_NAMES.map(attr => (
                              <Grid size={{ xs: 2 }} key={attr}>
                                <Box sx={{ textAlign: 'center' }}>
                                  <Typography variant="caption" color="text.secondary">{attr}</Typography>
                                  <Typography variant="h6">{attributes[attr]}</Typography>
                                  <Chip label={getModifier(attributes[attr])} size="small" />
                                </Box>
                              </Grid>
                            ))}
                          </Grid>
                        </Paper>

                        {startingEquipment.length > 0 && (
                          <Paper sx={{ p: 2 }}>
                            <Typography variant="subtitle2" gutterBottom>Equipment ({startingEquipment.length} items):</Typography>
                            <List dense>
                              {startingEquipment.map((item, i) => (
                                <ListItem key={i} sx={{ px: 0 }}>
                                  <ListItemText
                                    primary={`${item.quantity > 1 ? `${item.quantity}x ` : ''}${item.name}`}
                                    secondary={item.type}
                                  />
                                </ListItem>
                              ))}
                            </List>
                          </Paper>
                        )}

                        <Paper sx={{ p: 2 }}>
                          <Typography variant="subtitle2" gutterBottom>Proficiencies:</Typography>
                          <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                            {selectedTemplate.proficiencies.map(p => (
                              <Chip key={p} label={p} size="small" variant="outlined" color="primary" />
                            ))}
                          </Box>
                        </Paper>

                        <Paper sx={{ p: 2 }}>
                          <Typography variant="subtitle2" gutterBottom>Skills:</Typography>
                          <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                            {selectedTemplate.skills.map(s => (
                              <Chip key={s} label={s} size="small" variant="outlined" />
                            ))}
                          </Box>
                        </Paper>
                      </Box>
                    )}
                  </Box>
                )}
              </StepContent>
            </Step>
          ))}
        </Stepper>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        {step > 0 && (
          <Button onClick={handleBack} startIcon={<BackIcon />}>Back</Button>
        )}
        <Box sx={{ flex: 1 }} />
        {step < stepLabels.length - 1 ? (
          <Button onClick={handleNext} variant="contained" endIcon={<NextIcon />}>Next</Button>
        ) : (
          <Button onClick={handleFinish} variant="contained" color="success" startIcon={<SaveIcon />}>
            Create Character
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}
