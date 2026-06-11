import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useCharacter } from '../api/hooks/useGame';
import { Box, Typography, Paper, Tabs, Tab, Button, Chip, Slider, Alert } from '@mui/material';
import { ArrowBack as BackIcon, Healing as HPIcon, Shield as ShieldIcon, AutoFixHigh as StatsIcon, MenuBook as MenuBookIcon, ShoppingCart as InvIcon, AutoStories as SpellIcon, Warning as CondIcon } from '@mui/icons-material';
import CharacterOverviewTab from '../components/admin/CharacterOverviewTab';
import CharacterAttributesTab from '../components/admin/CharacterAttributesTab';
import CharacterSkillsTab from '../components/admin/CharacterSkillsTab';
import CharacterSpellsTab from '../components/admin/CharacterSpellsTab';
import CharacterInventoryTab from '../components/admin/CharacterInventoryTab';
import CharacterConditionsTab from '../components/admin/CharacterConditionsTab';

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

  const [_openSpellDialog, setOpenSpellDialog] = useState(false);
  const [_openItemDialog, setOpenItemDialog] = useState(false);
  const [_openCondDialog, setOpenCondDialog] = useState(false);
  const [_openLevelUp, _setOpenLevelUp] = useState(false);

  useEffect(() => {
    if (!character) return;
    setEditName(character.name);
    setEditClass(character.class);
    setEditLevel(character.level);
    setEditMaxHP(character.maxHP);
    setEditCurrentHP(character.currentHP);
    setEditProficiency(character.proficiencyBonus || 0);

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

    const skills: Record<string, number> = {};
    if (character.skills && typeof character.skills === 'object') {
      const skillObj = character.skills as Record<string, number>;
      for (const [key, val] of Object.entries(skillObj)) {
        if (typeof val === 'number') skills[key] = val;
      }
    }
    setEditSkills(skills);

    setEditSpells(character.spells && Array.isArray(character.spells) ? character.spells as any[] : []);
    setEditInventory(character.inventory && Array.isArray(character.inventory) ? character.inventory as any[] : []);
    setEditConditions(character.conditions && Array.isArray(character.conditions) ? character.conditions as any[] : []);
  }, [character]);

  const handleSave = async () => {
    if (!id) return;
    setErrorState(null);
    setSaveState(null);
    try {
      await updateCharacter({
        name: editName, class: editClass, level: editLevel,
        maxHP: editMaxHP, currentHP: editCurrentHP,
        attributes: { attributes: editAttributes }, skills: editSkills,
        proficiencyBonus: editProficiency, spells: editSpells,
        inventory: editInventory, conditions: editConditions,
      });
      setSaveState('Saved!');
      setEditMode(false);
      setTimeout(() => setSaveState(null), 2000);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleCancel = () => {
    if (!character) return;
    setEditName(character.name);
    setEditClass(character.class);
    setEditLevel(character.level);
    setEditMaxHP(character.maxHP);
    setEditCurrentHP(character.currentHP);
    setEditProficiency(character.proficiencyBonus || 0);
    setEditSpells(character.spells ? character.spells as any[] : []);
    setEditInventory(character.inventory ? character.inventory as any[] : []);
    setEditConditions(character.conditions ? character.conditions as any[] : []);
    setEditMode(false);
  };

  const getModifier = (value: number): string => {
    const mod = Math.floor((value - 10) / 2);
    return mod >= 0 ? `+${mod}` : `${mod}`;
  };
  void getModifier; // used in sub-components

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
  void getHPColor; // used in sub-components

  if (isLoading) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading...</Typography></Box>;
  if (!character) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Character not found</Typography></Box>;

  const tabs = [
    { label: 'Overview', icon: <StatsIcon /> },
    { label: 'Attributes', icon: <StatsIcon /> },
    { label: 'Skills', icon: <MenuBookIcon /> },
    { label: 'Spells', icon: <SpellIcon /> },
    { label: 'Inventory', icon: <InvIcon /> },
    { label: 'Conditions', icon: <CondIcon /> },
  ];

  return (
    <Box sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
        <Button startIcon={<BackIcon />} onClick={() => navigate(-1)}>Back</Button>
        <Typography variant="h4">{character.name}</Typography>
        <Chip label={`${editClass} Lv.${editLevel}`} size="small" />
        {editMode ? (
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Button variant="contained" onClick={handleSave}>Save</Button>
            <Button variant="outlined" onClick={handleCancel}>Cancel</Button>
          </Box>
        ) : (
          <Button variant="outlined" onClick={() => setEditMode(true)}>Edit</Button>
        )}
      </Box>

      {saveState && <Alert severity="success" onClose={() => setSaveState(null)} sx={{ mb: 2 }}>{saveState}</Alert>}
      {errorState && <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>{errorState}</Alert>}

      {/* HP Bar */}
      <Paper sx={{ p: 2, mb: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <HPIcon color="error" />
          <Box sx={{ flex: 1 }}>
            <Typography variant="body2" color="text.secondary">HP: {editCurrentHP}/{editMaxHP}</Typography>
            <Slider value={getHPPercent()} valueLabelDisplay="auto" valueLabelFormat={`${Math.round(getHPPercent())}%`} size="small" sx={{ mt: 0.5 }} />
          </Box>
          <ShieldIcon color="primary" />
          <Typography variant="body2">AC: {character.stats?.AC ?? '—'}</Typography>
        </Box>
      </Paper>

      <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)} sx={{ borderBottom: 1, borderColor: 'divider', mb: 2 }}>
        {tabs.map((tab, i) => (
          <Tab key={i} label={tab.label} icon={tab.icon} iconPosition="start" />
        ))}
      </Tabs>

      {activeTab === 0 && (
        <CharacterOverviewTab
          editName={editName} setEditName={setEditName}
          editClass={editClass} setEditClass={setEditClass}
          editLevel={editLevel} setEditLevel={setEditLevel}
          editMaxHP={editMaxHP} editCurrentHP={editCurrentHP}
          setEditCurrentHP={setEditCurrentHP}
          isEditMode={editMode} onSave={handleSave} onCancel={handleCancel}
        />
      )}
      {activeTab === 1 && <CharacterAttributesTab attributes={editAttributes} setAttributes={setEditAttributes} isEditMode={editMode} />}
      {activeTab === 2 && <CharacterSkillsTab skills={editSkills} setSkills={() => {}} proficiency={editProficiency} setProficiency={setEditProficiency} isEditMode={editMode} />}
      {activeTab === 3 && (
        <CharacterSpellsTab
          spells={editSpells} setSpells={setEditSpells}
          isEditMode={editMode}
          onOpenDialog={() => setOpenSpellDialog(true)}
          spellSlots={[]} onSlotChange={() => {}}
        />
      )}
      {activeTab === 4 && <CharacterInventoryTab items={editInventory} setItems={setEditInventory} isEditMode={editMode} onOpenDialog={() => setOpenItemDialog(true)} />}
      {activeTab === 5 && <CharacterConditionsTab conditions={editConditions} setConditions={setEditConditions} isEditMode={editMode} onOpenDialog={() => setOpenCondDialog(true)} />}
    </Box>
  );
}
