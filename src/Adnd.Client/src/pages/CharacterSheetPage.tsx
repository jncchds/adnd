import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useCharacter } from '../api/gameHooks';
import {
  Container, Box, Typography, Paper, Tabs, Tab,
  TextField, Button, IconButton, Chip,
  Divider, Dialog, DialogTitle, DialogContent, DialogActions,
  List, ListItem, ListItemText,
  MenuItem, Select, FormControl, InputLabel,
  Slider
} from '@mui/material';
import {
  Add as AddIcon, Delete as DeleteIcon,
  Edit as EditIcon, Save as SaveIcon,
  ArrowBack as BackIcon, Healing as HPIcon,
  Shield as ShieldIcon, AutoFixHigh as StatsIcon,
  MenuBook as MenuBookIcon, ShoppingCart as InvIcon, AutoStories as SpellIcon,
  Warning as CondIcon
} from '@mui/icons-material';

export default function CharacterSheetPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { character, isLoading, updateCharacter } = useCharacter(id);

  const [activeTab, setActiveTab] = useState(0);
  const [editMode, setEditMode] = useState(false);
  const [saveState, setSaveState] = useState<string | null>(null);
  const [errorState, setErrorState] = useState<string | null>(null);

  // Edit state
  const [editName, setEditName] = useState('');
  const [editClass, setEditClass] = useState('');
  const [editLevel, setEditLevel] = useState(1);
  const [editMaxHP, setEditMaxHP] = useState(10);
  const [editCurrentHP, setEditCurrentHP] = useState(10);
  const [editAttributes, setEditAttributes] = useState<Record<string, number>>({});
  const [editSkills, setEditSkills] = useState<Record<string, number>>({});
  const [editProficiency, setEditProficiency] = useState(0);
  const [editSpells, setEditSpells] = useState<any[]>([]);
  const [editInventory, setEditInventory] = useState<any[]>([]);
  const [editConditions, setEditConditions] = useState<any[]>([]);

  // Dialog states
  const [openSpellDialog, setOpenSpellDialog] = useState(false);
  const [openItemDialog, setOpenItemDialog] = useState(false);
  const [openCondDialog, setOpenCondDialog] = useState(false);
  const [openLevelUp, setOpenLevelUp] = useState(false);

  // Spell/Item/Condition editing
  const [spellName, setSpellName] = useState('');
  const [spellLevel, setSpellLevel] = useState(0);
  const [spellPrepared, setSpellPrepared] = useState(true);
  const [spellSlotsUsed, setSpellSlotsUsed] = useState(0);
  const [spellDesc, setSpellDesc] = useState('');

  const [itemName, setItemName] = useState('');
  const [itemDesc, setItemDesc] = useState('');
  const [itemWeight, setItemWeight] = useState(0);

  const [condName, setCondName] = useState('');
  const [condDesc, setCondDesc] = useState('');

  useEffect(() => {
    if (character) {
      setEditName(character.name);
      setEditClass(character.class);
      setEditLevel(character.level);
      setEditMaxHP(character.maxHP);
      setEditCurrentHP(character.currentHP);
      setEditProficiency(character.proficiencyBonus || 0);

      // Parse attributes
      const attrs: Record<string, number> = {};
      if (character.attributes && typeof character.attributes === 'object') {
        const attrObj = character.attributes as any;
        if (attrObj.attributes && typeof attrObj.attributes === 'object') {
          for (const [key, val] of Object.entries(attrObj.attributes)) {
            if (typeof val === 'number') attrs[key] = val;
          }
        }
      }
      setEditAttributes(attrs);

      // Parse skills
      const skills: Record<string, number> = {};
      if (character.skills && typeof character.skills === 'object') {
        const skillObj = character.skills as Record<string, number>;
        for (const [key, val] of Object.entries(skillObj)) {
          if (typeof val === 'number') skills[key] = val;
        }
      }
      setEditSkills(skills);

      // Parse spells
      if (character.spells && Array.isArray(character.spells)) {
        setEditSpells(character.spells as any[]);
      } else {
        setEditSpells([]);
      }

      // Parse inventory
      if (character.inventory && Array.isArray(character.inventory)) {
        setEditInventory(character.inventory as any[]);
      } else {
        setEditInventory([]);
      }

      // Parse conditions
      if (character.conditions && Array.isArray(character.conditions)) {
        setEditConditions(character.conditions as any[]);
      } else {
        setEditConditions([]);
      }
    }
  }, [character]);

  const handleSave = async () => {
    if (!id) return;
    setErrorState(null);
    setSaveState(null);
    try {
      // Build attributes JSON
      const attrJson = { attributes: editAttributes };

      // Build skills JSON
      const skillsJson = editSkills;

      await updateCharacter({
        name: editName,
        class: editClass,
        level: editLevel,
        maxHP: editMaxHP,
        currentHP: editCurrentHP,
        attributes: attrJson,
        skills: skillsJson,
        proficiencyBonus: editProficiency,
        spells: editSpells,
        inventory: editInventory,
        conditions: editConditions,
      });
      setSaveState('Saved successfully!');
      setEditMode(false);
      setTimeout(() => setSaveState(null), 2000);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleCancel = () => {
    if (character) {
      setEditName(character.name);
      setEditClass(character.class);
      setEditLevel(character.level);
      setEditMaxHP(character.maxHP);
      setEditCurrentHP(character.currentHP);
      setEditProficiency(character.proficiencyBonus || 0);
    }
    setEditMode(false);
    setEditSpells(character?.spells ? character.spells as any[] : []);
    setEditInventory(character?.inventory ? character.inventory as any[] : []);
    setEditConditions(character?.conditions ? character.conditions as any[] : []);
  };

  const getModifier = (value: number): string => {
    const mod = Math.floor((value - 10) / 2);
    return mod >= 0 ? `+${mod}` : `${mod}`;
  };
  void getModifier; // used in components

  const getHPPercent = (): number => {
    if (editMaxHP <= 0) return 0;
    return Math.max(0, Math.min(100, (editCurrentHP / editMaxHP) * 100));
  };

  const getHPColor = (): string => {
    const pct = getHPPercent();
    if (pct > 60) return '#4caf50';
    if (pct > 30) return '#ff9800';
    return '#f44336';
  };

  const hpColor = getHPColor();

  const tabs = [
    { label: 'Overview', icon: <ShieldIcon /> },
    { label: 'Attributes', icon: <StatsIcon /> },
    { label: 'Skills', icon: <MenuBookIcon /> },
    { label: 'Spells', icon: <SpellIcon /> },
    { label: 'Inventory', icon: <InvIcon /> },
    { label: 'Conditions', icon: <CondIcon /> },
  ];
  void SpellIcon; // used in tab icons

  if (isLoading) {
    return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading character...</Typography></Box>;
  }

  return (
    <Container maxWidth="md" sx={{ mt: 2, mb: 4 }}>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <IconButton onClick={() => navigate(-1)} size="small">
            <BackIcon />
          </IconButton>
          <Box>
            <Typography variant="h5">{editName || 'Unnamed Character'}</Typography>
            <Box sx={{ display: 'flex', gap: 1, mt: 0.5 }}>
              <Chip label={`${editClass || 'Class'} · Lv.${editLevel}`} size="small" />
              <Chip label={character?.systemId ?? 'D&D 5e'} size="small" />
            </Box>
          </Box>
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          {saveState && <Chip label={saveState} size="small" color="success" />}
          {errorState && <Chip label={errorState} size="small" color="error" />}
          {!editMode && (
            <Button variant="outlined" startIcon={<EditIcon />} onClick={() => setEditMode(true)}>
              Edit
            </Button>
          )}
          {editMode && (
            <>
              <Button variant="outlined" onClick={handleCancel}>Cancel</Button>
              <Button variant="contained" startIcon={<SaveIcon />} onClick={handleSave}>
                Save
              </Button>
            </>
          )}
        </Box>
      </Box>

      {/* HP Bar */}
      <Paper sx={{ p: 2, mb: 2, bgcolor: 'background.default' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <HPIcon sx={{ color: hpColor }} />
          <Box sx={{ flex: 1 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
              <Typography variant="body2" color="text.secondary">Hit Points</Typography>
              <Typography variant="body2" sx={{ color: hpColor, fontWeight: 'bold' }}>
                {editCurrentHP} / {editMaxHP}
              </Typography>
            </Box>
            <Slider
              value={getHPPercent()}
              onChange={(_, val) => {
                const pct = val as number;
                setEditCurrentHP(Math.round((pct / 100) * editMaxHP));
              }}
              sx={{ color: hpColor }}
              disabled={!editMode}
              size="small"
            />
          </Box>
          {editMode && (
            <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
              <TextField
                size="small"
                type="number"
                label="HP"
                value={editCurrentHP}
                onChange={e => setEditCurrentHP(parseInt(e.target.value) || 0)}
                sx={{ width: 80 }}
                inputProps={{ min: 0, max: editMaxHP }}
              />
              <TextField
                size="small"
                type="number"
                label="Max"
                value={editMaxHP}
                onChange={e => setEditMaxHP(parseInt(e.target.value) || 1)}
                sx={{ width: 80 }}
                inputProps={{ min: 1 }}
              />
            </Box>
          )}
        </Box>
      </Paper>

      {/* Tabs */}
      <Paper sx={{ mb: 2 }}>
        <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)}>
          {tabs.map((tab, i) => (
            <Tab key={i} label={tab.label} icon={tab.icon} iconPosition="start" />
          ))}
        </Tabs>
      </Paper>

      {/* Tab Content */}
      <Box>
        {activeTab === 0 && <OverviewTab editName={editName} setEditName={setEditName} editClass={editClass} setEditClass={setEditClass} editLevel={editLevel} setEditLevel={setEditLevel} editMaxHP={editMaxHP} editCurrentHP={editCurrentHP} setEditCurrentHP={setEditCurrentHP} isEditMode={editMode} onSave={handleSave} onCancel={handleCancel} />}

        {activeTab === 1 && <AttributesTab attributes={editAttributes} setAttributes={setEditAttributes} isEditMode={editMode} />}

        {activeTab === 2 && <SkillsTab skills={editSkills} setSkills={setEditSkills} proficiency={editProficiency} setProficiency={setEditProficiency} isEditMode={editMode} />}

        {activeTab === 3 && (
          <SpellsTab
            spells={editSpells}
            setSpells={setEditSpells}
            isEditMode={editMode}
            onOpenDialog={() => setOpenSpellDialog(true)}
          />
        )}

        {activeTab === 4 && (
          <InventoryTab
            items={editInventory}
            setItems={setEditInventory}
            isEditMode={editMode}
            onOpenDialog={() => setOpenItemDialog(true)}
          />
        )}

        {activeTab === 5 && <ConditionsTab conditions={editConditions} setConditions={setEditConditions} isEditMode={editMode} onOpenDialog={() => setOpenCondDialog(true)} />}
      </Box>

      {/* Spell Dialog */}
      <Dialog open={openSpellDialog} onClose={() => setOpenSpellDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Spell</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField fullWidth label="Spell Name" value={spellName} onChange={e => setSpellName(e.target.value)} autoFocus />
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField fullWidth size="small" label="Level" type="number" value={spellLevel} onChange={e => setSpellLevel(parseInt(e.target.value) || 0)} inputProps={{ min: 0, max: 9 }} />
            <FormControl size="small" sx={{ flex: 1 }}>
              <InputLabel>Prepared</InputLabel>
              <Select
                value={spellPrepared ? 'true' : 'false'}
                label="Prepared"
                onChange={e => setSpellPrepared(e.target.value === 'true')}
              >
                <MenuItem value="true">Yes</MenuItem>
                <MenuItem value="false">No</MenuItem>
              </Select>
            </FormControl>
            <TextField fullWidth size="small" label="Slots Used" type="number" value={spellSlotsUsed} onChange={e => setSpellSlotsUsed(parseInt(e.target.value) || 0)} inputProps={{ min: 0 }} />
          </Box>
          <TextField fullWidth label="Description" multiline rows={2} value={spellDesc} onChange={e => setSpellDesc(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenSpellDialog(false)}>Cancel</Button>
          <Button onClick={() => {
            if (!spellName.trim()) return;
            setEditSpells(prev => [...prev, {
              name: spellName,
              level: spellLevel,
              prepared: spellPrepared,
              slotsUsed: spellSlotsUsed,
              description: spellDesc,
            }]);
            setOpenSpellDialog(false);
            setSpellName(''); setSpellLevel(0); setSpellPrepared(true); setSpellSlotsUsed(0); setSpellDesc('');
          }} variant="contained" disabled={!spellName.trim()}>
            Add Spell
          </Button>
        </DialogActions>
      </Dialog>

      {/* Item Dialog */}
      <Dialog open={openItemDialog} onClose={() => setOpenItemDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Item</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField fullWidth label="Item Name" value={itemName} onChange={e => setItemName(e.target.value)} autoFocus />
          <TextField fullWidth label="Description" multiline rows={2} value={itemDesc} onChange={e => setItemDesc(e.target.value)} />
          <TextField fullWidth size="small" label="Weight" type="number" value={itemWeight} onChange={e => setItemWeight(parseFloat(e.target.value) || 0)} inputProps={{ step: 0.1, min: 0 }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenItemDialog(false)}>Cancel</Button>
          <Button onClick={() => {
            if (!itemName.trim()) return;
            setEditInventory(prev => [...prev, {
              name: itemName,
              description: itemDesc,
              weight: itemWeight,
            }]);
            setOpenItemDialog(false);
            setItemName(''); setItemDesc(''); setItemWeight(0);
          }} variant="contained" disabled={!itemName.trim()}>
            Add Item
          </Button>
        </DialogActions>
      </Dialog>

      {/* Condition Dialog */}
      <Dialog open={openCondDialog} onClose={() => setOpenCondDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Condition</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField fullWidth label="Condition Name" value={condName} onChange={e => setCondName(e.target.value)} autoFocus />
          <TextField fullWidth label="Description" multiline rows={2} value={condDesc} onChange={e => setCondDesc(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenCondDialog(false)}>Cancel</Button>
          <Button onClick={() => {
            if (!condName.trim()) return;
            setEditConditions(prev => [...prev, {
              name: condName,
              description: condDesc,
              addedAt: new Date().toISOString(),
            }]);
            setOpenCondDialog(false);
            setCondName(''); setCondDesc('');
          }} variant="contained" disabled={!condName.trim()}>
            Add Condition
          </Button>
        </DialogActions>
      </Dialog>

      {/* Level Up Dialog */}
      <Dialog open={openLevelUp} onClose={() => setOpenLevelUp(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Level Up</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Level up {editName} from level {editLevel} to level {editLevel + 1}.
            Update HP and other stats after leveling up.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenLevelUp(false)}>Cancel</Button>
          <Button onClick={() => {
            setEditLevel(prev => prev + 1);
            setOpenLevelUp(false);
          }} variant="contained">
            Level Up
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}

// ==================== Sub-Components ====================

function OverviewTab({ editName, setEditName, editClass, setEditClass, editLevel, setEditLevel, editMaxHP, editCurrentHP, setEditCurrentHP, isEditMode, onSave, onCancel }: any) {
  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
      <Box sx={{ flex: '1 1 300px' }}>
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle1" color="primary" gutterBottom>Character Info</Typography>
          {isEditMode ? (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
              <TextField fullWidth label="Name" value={editName} onChange={e => setEditName(e.target.value)} size="small" />
              <TextField fullWidth select label="Class" value={editClass} onChange={e => setEditClass(e.target.value)} size="small">
                {['Fighter', 'Wizard', 'Rogue', 'Cleric', 'Ranger', 'Barbarian', 'Bard', 'Druid', 'Monk', 'Paladin', 'Sorcerer', 'Warlock'].map(c => (
                  <MenuItem key={c} value={c}>{c}</MenuItem>
                ))}
              </TextField>
              <Box sx={{ display: 'flex', gap: 1 }}>
                <TextField fullWidth size="small" label="Level" type="number" value={editLevel} onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEditLevel(parseInt(e.target.value) || 1)} inputProps={{ min: 1 }} />
                <Button size="small" variant="outlined" onClick={() => setEditLevel((l: number) => l + 1)}>+1</Button>
              </Box>
            </Box>
          ) : (
            <Box>
              <Typography variant="body2">Class: <strong>{editClass}</strong></Typography>
              <Typography variant="body2">Level: <strong>{editLevel}</strong></Typography>
            </Box>
          )}
        </Paper>
      </Box>
      <Box sx={{ flex: '1 1 300px' }}>
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle1" color="primary" gutterBottom>Quick Actions</Typography>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            <Button size="small" variant="outlined" onClick={onSave} disabled={!isEditMode}>
              Save Changes
            </Button>
            <Button size="small" variant="outlined" onClick={onCancel} disabled={!isEditMode}>
              Cancel
            </Button>
          </Box>
        </Paper>
      </Box>
      <Box sx={{ width: '100%' }}>
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle1" color="primary" gutterBottom>HP Quick Adjust</Typography>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
            <Typography variant="body2">Current: {editCurrentHP} / {editMaxHP}</Typography>
            {isEditMode && (
              <>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP((prev: number) => Math.max(0, prev - 1))}>-1</Button>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP((prev: number) => Math.max(0, prev - 5))}>-5</Button>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP((prev: number) => prev + 1)}>+1</Button>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP((prev: number) => prev + 5)}>+5</Button>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP(editMaxHP)}>Full Heal</Button>
                <Button size="small" variant="outlined" color="error" onClick={() => setEditCurrentHP(0)}>Die</Button>
              </>
            )}
          </Box>
        </Paper>
      </Box>
    </Box>
  );
}

function AttributesTab({ attributes, setAttributes, isEditMode }: any) {
  const attrNames = Object.keys(attributes).sort();

  if (attrNames.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ textAlign: 'center', py: 4 }}>
        No attributes defined. Click "Edit" to add attributes.
      </Typography>
    );
  }

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
      {attrNames.map(attr => {
        const value = attributes[attr] || 10;
        const mod = Math.floor((value - 10) / 2);
        return (
          <Box key={attr} sx={{ flex: '1 1 200px' }}>
            <Paper sx={{ p: 2, textAlign: 'center' }}>
              <Typography variant="subtitle1" textTransform="capitalize" sx={{ mb: 1 }}>
                {attr.replace(/([A-Z])/g, ' $1').trim()}
              </Typography>
              {isEditMode ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', gap: 1, alignItems: 'center' }}>
                  <IconButton size="small" onClick={() => setAttributes((prev: Record<string, number>) => ({ ...prev, [attr]: Math.max(1, (prev[attr] || 10) - 1) }))}>
                    -
                  </IconButton>
                  <Typography variant="h4">{value}</Typography>
                  <IconButton size="small" onClick={() => setAttributes((prev: Record<string, number>) => ({ ...prev, [attr]: (prev[attr] || 10) + 1 }))}>
                    +
                  </IconButton>
                </Box>
              ) : (
                <Typography variant="h4">{value}</Typography>
              )}
              <Chip
                label={mod >= 0 ? `+${mod}` : `${mod}`}
                size="small"
                sx={{ mt: 1, fontWeight: 'bold', fontSize: '1.1rem' }}
                color={mod >= 3 ? 'success' : mod <= -3 ? 'error' : 'default'}
              />
            </Paper>
          </Box>
        );
      })}
    </Box>
  );
}

function SkillsTab({ skills, setSkills: _setSkills, proficiency, setProficiency, isEditMode }: any) {
  void _setSkills; // used when skills become editable
  const skillNames = Object.keys(skills).sort();

  if (skillNames.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ textAlign: 'center', py: 4 }}>
        No skills defined.
      </Typography>
    );
  }

  return (
    <List>
      {skillNames.map(name => {
        const val = skills[name] || 0;
        const displayVal = val;
        const isProficient = val > 0 && val > (skills[name]?.base || 0);
        return (
          <ListItem key={name} sx={{ px: 0, borderBottom: 1, borderColor: 'divider' }}>
            <ListItemText
              primary={name}
              secondary={
                isProficient
                  ? `Proficient (+${proficiency})`
                  : 'Untrained'
              }
            />
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              {isProficient && <Chip label="Prof" size="small" color="primary" variant="outlined" />}
              <Typography variant="h6" sx={{ minWidth: 30, textAlign: 'right' }}>
                {displayVal >= 0 ? `+${displayVal}` : displayVal}
              </Typography>
            </Box>
          </ListItem>
        );
      })}
      {isEditMode && (
        <ListItem sx={{ px: 0, bgcolor: 'action.hover' }}>
          <ListItemText
            primary="Proficiency Bonus"
            secondary="Global modifier for proficient skills"
          />
          <TextField
            size="small"
            type="number"
            value={proficiency}
            onChange={e => setProficiency(parseInt(e.target.value) || 0)}
            sx={{ width: 80 }}
            inputProps={{ min: 0, max: 10 }}
          />
        </ListItem>
      )}
    </List>
  );
}

function SpellsTab({ spells, setSpells, isEditMode, onOpenDialog }: any) {
  const spellLevels = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

  const groupedSpells = spellLevels.reduce((acc: any, level) => {
    const levelSpells = spells.filter((s: any) => s.level === level);
    if (levelSpells.length > 0) {
      acc[level] = levelSpells;
    }
    return acc;
  }, {});

  const totalSpells = spells.length;
  const preparedSpells = spells.filter((s: any) => s.prepared).length;

  if (totalSpells === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 4 }}>
        <Typography color="text.secondary" sx={{ mb: 2 }}>No spells defined.</Typography>
        {isEditMode && (
          <Button variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Spell
          </Button>
        )}
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="body2" color="text.secondary">
          {preparedSpells} / {totalSpells} spells prepared
        </Typography>
        {isEditMode && (
          <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Spell
          </Button>
        )}
      </Box>
      <Divider sx={{ mb: 2 }} />
      {spellLevels.filter(l => groupedSpells[l]).map(level => (
        <Box key={level} sx={{ mb: 2 }}>
          <Typography variant="subtitle2" color="primary" sx={{ mb: 1 }}>
            {level === 0 ? 'Cantrips' : `${level}st Level`}
          </Typography>
          <List dense>
            {groupedSpells[level].map((spell: any, i: number) => (
              <ListItem key={i} sx={{ px: 0, borderBottom: 1, borderColor: 'divider' }}>
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      {spell.prepared && <Chip label="✓" size="small" color="success" variant="outlined" />}
                      <Typography variant="body2">{spell.name}</Typography>
                    </Box>
                  }
                  secondary={spell.description || 'No description'}
                />
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  {spell.slotsUsed > 0 && (
                    <Chip label={`${spell.slotsUsed} slot${spell.slotsUsed > 1 ? 's' : ''}`} size="small" color="warning" variant="outlined" />
                  )}
                  {isEditMode && (
                    <IconButton size="small" color="error" onClick={() => setSpells((prev: any) => prev.filter((_: any, idx: number) => idx !== i))}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  )}
                </Box>
              </ListItem>
            ))}
          </List>
        </Box>
      ))}
    </Box>
  );
}

function InventoryTab({ items, setItems, isEditMode, onOpenDialog }: any) {
  const totalWeight = items.reduce((sum: number, item: any) => sum + (item.weight || 0), 0);

  if (items.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 4 }}>
        <Typography color="text.secondary" sx={{ mb: 2 }}>No items in inventory.</Typography>
        {isEditMode && (
          <Button variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Item
          </Button>
        )}
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="body2" color="text.secondary">
          {items.length} item{items.length !== 1 ? 's' : ''} · {totalWeight.toFixed(1)} weight
        </Typography>
        {isEditMode && (
          <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Item
          </Button>
        )}
      </Box>
      <Divider sx={{ mb: 2 }} />
      <List dense>
        {items.map((item: any, i: number) => (
          <ListItem key={i} sx={{ px: 0, borderBottom: 1, borderColor: 'divider' }}>
            <ListItemText
              primary={item.name}
              secondary={item.description || `${item.weight ? item.weight + ' weight' : ''}`}
            />
            {isEditMode && (
              <IconButton size="small" color="error" onClick={() => setItems((prev: any) => prev.filter((_: any, idx: number) => idx !== i))}>
                <DeleteIcon fontSize="small" />
              </IconButton>
            )}
          </ListItem>
        ))}
      </List>
    </Box>
  );
}

function ConditionsTab({ conditions, setConditions, isEditMode, onOpenDialog }: any) {
  const conditionIcons: Record<string, string> = {
    'blinded': '👁️',
    'deafened': '👂',
    'frightened': '😨',
    'grappled': '🤝',
    'incapacitated': '🚫',
    'invisible': '👻',
    'paralyzed': '💎',
    'petrified': '🗿',
    'poisoned': '☠️',
    'prone': '🤸',
    'restrained': '🔗',
    'stunned': '💫',
    'unconscious': '😴',
  };

  if (conditions.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 4 }}>
        <Typography color="text.secondary" sx={{ mb: 2 }}>No active conditions.</Typography>
        {isEditMode && (
          <Button variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Condition
          </Button>
        )}
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="body2" color="text.secondary">
          {conditions.length} condition{conditions.length !== 1 ? 's' : ''}
        </Typography>
        {isEditMode && (
          <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Condition
          </Button>
        )}
      </Box>
      <Divider sx={{ mb: 2 }} />
      <List dense>
        {conditions.map((cond: any, i: number) => {
          const icon = conditionIcons[cond.name?.toLowerCase()] || '⚠️';
          return (
            <ListItem key={i} sx={{ px: 0, borderBottom: 1, borderColor: 'divider' }}>
              <ListItemText
                primary={
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Typography>{icon}</Typography>
                    <Typography variant="body2">{cond.name}</Typography>
                  </Box>
                }
                secondary={cond.description || ''}
              />
              {isEditMode && (
                <IconButton size="small" color="error" onClick={() => setConditions((prev: any) => prev.filter((_: any, idx: number) => idx !== i))}>
                  <DeleteIcon fontSize="small" />
                </IconButton>
              )}
            </ListItem>
          );
        })}
      </List>
    </Box>
  );
}
