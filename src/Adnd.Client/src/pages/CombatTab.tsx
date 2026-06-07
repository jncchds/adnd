import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useAuth } from '../api/authHook';
import { useGameHub } from '../api/hubHook';
import {
  Box, Typography, Paper, Button, IconButton,
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Chip, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Alert, Collapse, MenuItem, Select,
  FormControl, InputLabel, Grid, Divider, Avatar,
  Tooltip,
} from '@mui/material';
import {
  DirectionsRun as CombatIcon, Replay as TurnIcon, People as PeopleIcon,
  Add as AddIcon, Remove as RemoveIcon, HealthAndSafety as HPIcon,
  Shield as ACIcon, EmojiEvents as InitiativeIcon,
  ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon,
  Gavel as AttackIcon, LocalHospital as HealIcon,
  Warning as ConditionIcon, AutoFixNormal as DeathSaveIcon,
  Refresh as RefreshIcon, Pause as PauseIcon, PlayArrow as PlayIcon,
  History as HistoryIcon, AutoAwesome as SpellIcon,
  Psychology as AICogIcon, Bed as RestIcon,
  Map as GridIcon, Inventory as InventoryIcon,
  Star as StarIcon,
} from '@mui/icons-material';
import {
  type CombatLog, type CombatParticipantSummary,
  type ConditionEntry, type CombatLogEvent,
  type SpellCastResult, type AISuggestions, type SANCheckResult,
} from '../types';

interface CombatTabProps {
  gameId: string;
}

export default function CombatTab({ gameId }: CombatTabProps) {
  const { id: _id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const { isConnected, invoke, on } = useGameHub();

  const [activeCombat, setActiveCombat] = useState<CombatLog | null>(null);
  const [activeCombats, setActiveCombats] = useState<any[]>([]);
  const [showCombatLog, setShowCombatLog] = useState(false);
  const [showAddParticipant, setShowAddParticipant] = useState(false);
  const [showAttackDialog, setShowAttackDialog] = useState(false);
  const [showSaveThrowDialog, setShowSaveThrowDialog] = useState(false);
  const [showConditionDialog, setShowConditionDialog] = useState(false);
  const [showDeathSaveDialog, setShowDeathSaveDialog] = useState(false);
  const [showHealDialog, setShowHealDialog] = useState(false);
  const [showRollInitiative, setShowRollInitiative] = useState(false);
  const [showSpellDialog, setShowSpellDialog] = useState(false);
  const [showAISuggestions, setShowAISuggestions] = useState(false);
  const [showRestDialog, setShowRestDialog] = useState(false);
  const [showGridDialog, setShowGridDialog] = useState(false);
  const [showInventoryDialog, setShowInventoryDialog] = useState(false);
  const [showSANDialog, setShowSANDialog] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [aiSuggestions, setAISuggestions] = useState<AISuggestions | null>(null);

  const [attackTarget, setAttackTarget] = useState('');
  const [attackFormula, setAttackFormula] = useState('1d20');
  const [attackBonus, setAttackBonus] = useState(0);
  const [damageFormula, setDamageFormula] = useState('1d8');
  const [damageBonus, setDamageBonus] = useState(0);
  const [weaponName, setWeaponName] = useState('Longsword');

  const [saveTarget, setSaveTarget] = useState('');
  const [saveType, setSaveType] = useState('Fortitude');
  const [saveFormula, setSaveFormula] = useState('1d20');
  const [saveDC, setSaveDC] = useState(15);

  const [conditionTarget, setConditionTarget] = useState('');
  const [conditionName, setConditionName] = useState('');
  const [conditionDuration, setConditionDuration] = useState(1);

  const [deathSaveTarget, setDeathSaveTarget] = useState('');

  const [healTarget, setHealTarget] = useState('');
  const [healAmount, setHealAmount] = useState(1);

  const [spellTarget, setSpellTarget] = useState('');
  const [spellName, setSpellName] = useState('');
  const [spellLevel, setSpellLevel] = useState('1');
  const [spellSaveFormula, setSpellSaveFormula] = useState('1d20');
  const [spellSaveDC, setSpellSaveDC] = useState(15);
  const [spellDamageFormula, setSpellDamageFormula] = useState('1d6');
  const [spellDamageBonus, setSpellDamageBonus] = useState(0);
  const [spellDescription, setSpellDescription] = useState('');

  const [newParticipantType, setNewParticipantType] = useState('NPC');
  const [newDisplayName, setNewDisplayName] = useState('');
  const [newAC, setNewAC] = useState(10);
  const [newHP, setNewHP] = useState(10);
  const [newMaxHP, setNewMaxHP] = useState(10);

  const [gridWidth, setGridWidth] = useState(20);
  const [gridHeight, setGridHeight] = useState(15);

  const [sanTarget, setSanTarget] = useState('');
  const [sanLoss, setSanLoss] = useState(1);
  const [sanRecovery, setSanRecovery] = useState(1);
  const [sanDC, setSanDC] = useState(20);

  const [invItemName, setInvItemName] = useState('');
  const [invItemType, setInvItemType] = useState('Weapon');
  const [invQuantity, setInvQuantity] = useState(1);

  useEffect(() => { loadActiveCombats(); }, [gameId]);

  useEffect(() => {
    if (!isConnected) return;
    on('CombatStarted', (data: any) => { setActiveCombat(data); loadActiveCombats(); });
    on('CombatEnded', () => { setActiveCombat(null); loadActiveCombats(); });
    on('CombatPaused', () => loadActiveCombat());
    on('CombatResumed', () => loadActiveCombat());
    on('CombatParticipantAdded', () => loadActiveCombat());
    on('CombatParticipantRemoved', () => loadActiveCombat());
    on('InitiativeRolled', () => loadActiveCombat());
    on('InitiativeComplete', () => loadActiveCombat());
    on('TurnAdvanced', () => loadActiveCombat());
    on('TurnSet', () => loadActiveCombat());
    on('ConditionApplied', () => loadActiveCombat());
    on('ConditionRemoved', () => loadActiveCombat());
    on('CombatLogUpdated', (data: { combatLog: CombatLog }) => { setActiveCombat(data.combatLog); });
    on('CombatSpellCast', (data: SpellCastResult) => {
      setSuccess(`${data.caster} cast ${data.spellName} on ${data.target}!`);
      setTimeout(() => setSuccess(null), 3000); loadActiveCombat();
    });
    on('CombatAISuggestions', (data: AISuggestions) => { setAISuggestions(data); });
    on('CombatAINPCBehavior', () => loadActiveCombat());
    on('CombatRestStarted', () => loadActiveCombat());
    on('CombatRestEnded', () => loadActiveCombat());
    on('CombatSANCheck', (data: SANCheckResult) => {
      setSuccess(`${data.participant} SAN: ${data.roll} vs ${data.dc} -> ${data.success ? 'SUCCESS' : 'FAILURE'} (-${data.sanLoss} SAN)`);
      setTimeout(() => setSuccess(null), 3000); loadActiveCombat();
    });
    on('CombatXP', () => loadActiveCombat());
    on('CombatLevelUp', () => loadActiveCombat());
    on('CombatItemAdded', () => loadActiveCombat());
    on('CombatItemRemoved', () => loadActiveCombat());
    on('CombatItemEquipped', () => loadActiveCombat());
    on('CombatItemUnequipped', () => loadActiveCombat());
    on('CombatGridSet', () => loadActiveCombat());
    on('CombatPositionSet', () => loadActiveCombat());
    on('CombatMove', () => loadActiveCombat());
    on('CombatAutoResolved', () => loadActiveCombat());
    return () => {};
  }, [isConnected, on, activeCombat]);

  const loadActiveCombats = async () => {
    try {
      const combats = await invoke('GetActiveCombats', gameId);
      if (combats) setActiveCombats(combats);
    } catch { /* ignore */ }
  };

  const loadActiveCombat = async () => {
    if (activeCombats.length === 0) { setActiveCombat(null); return; }
    try {
      const log = await invoke('GetCombatLog', activeCombats[0].id);
      if (log) setActiveCombat(log);
    } catch { /* ignore */ }
  };

  const handleStartCombat = async () => {
    try {
      const log = await invoke('StartCombat', gameId);
      if (log) { setActiveCombat(log); setSuccess('Combat started!'); setTimeout(() => setSuccess(null), 3000); }
    } catch (e: any) { setError(e.message); }
  };

  const handleEndCombat = async () => {
    if (!activeCombat) return;
    try { await invoke('EndCombat', activeCombat.combatId, 'Combat ended'); setActiveCombat(null); setSuccess('Combat ended!'); setTimeout(() => setSuccess(null), 3000); }
    catch (e: any) { setError(e.message); }
  };

  const handlePauseCombat = async () => {
    if (!activeCombat) return;
    try { await invoke('PauseCombat', activeCombat.combatId); setSuccess('Combat paused'); setTimeout(() => setSuccess(null), 2000); }
    catch (e: any) { setError(e.message); }
  };

  const handleResumeCombat = async () => {
    if (!activeCombat) return;
    try { await invoke('ResumeCombat', activeCombat.combatId); setSuccess('Combat resumed'); setTimeout(() => setSuccess(null), 2000); }
    catch (e: any) { setError(e.message); }
  };

  const handleAddParticipant = async () => {
    if (!activeCombat) return;
    try {
      await invoke('AddParticipant', activeCombat.combatId, newParticipantType, newDisplayName, newAC, newHP, newMaxHP);
      setShowAddParticipant(false); setNewDisplayName(''); setSuccess(`Added ${newDisplayName}`); setTimeout(() => setSuccess(null), 2000);
    } catch (e: any) { setError(e.message); }
  };

  const handleRemoveParticipant = async (participantId: string) => {
    if (!activeCombat) return;
    try { await invoke('RemoveParticipant', activeCombat.combatId, participantId); setSuccess('Participant removed'); setTimeout(() => setSuccess(null), 2000); }
    catch (e: any) { setError(e.message); }
  };

  const handleRollInitiative = async () => {
    if (!activeCombat) return;
    try { await invoke('RollInitiativeForAll', activeCombat.combatId); setShowRollInitiative(false); setSuccess('Initiative rolled!'); setTimeout(() => setSuccess(null), 2000); }
    catch (e: any) { setError(e.message); }
  };

  const handleAdvanceTurn = async () => { if (!activeCombat) return; try { await invoke('AdvanceTurn', activeCombat.combatId); } catch (e: any) { setError(e.message); } };

  const handleAttack = async () => {
    if (!activeCombat || !attackTarget) return;
    try {
      await invoke('CombatAttack', activeCombat.combatId, user?.displayName || 'Player', weaponName, attackTarget, attackFormula, attackBonus, damageFormula, damageBonus);
      setShowAttackDialog(false); setSuccess('Attack resolved!'); setTimeout(() => setSuccess(null), 2000);
    } catch (e: any) { setError(e.message); }
  };

  const handleSaveThrow = async () => {
    if (!activeCombat || !saveTarget) return;
    try {
      const participant = activeCombat.participants.find((p: any) => p.id === saveTarget);
      await invoke('CombatSaveThrow', activeCombat.combatId, participant?.displayName || 'Unknown', saveTarget, saveType, saveFormula, saveDC);
      setShowSaveThrowDialog(false); setSuccess(`${saveType} save resolved!`); setTimeout(() => setSuccess(null), 2000);
    } catch (e: any) { setError(e.message); }
  };

  const handleApplyCondition = async () => {
    if (!activeCombat || !conditionTarget || !conditionName) return;
    try {
      await invoke('CombatApplyCondition', activeCombat.combatId, conditionTarget, conditionName, conditionDuration > 0 ? conditionDuration : null);
      setShowConditionDialog(false); setSuccess(`Applied ${conditionName}`); setTimeout(() => setSuccess(null), 2000);
    } catch (e: any) { setError(e.message); }
  };

  const handleDeathSave = async (successVal: boolean) => {
    if (!activeCombat || !deathSaveTarget) return;
    try {
      await invoke('CombatDeathSave', activeCombat.combatId, deathSaveTarget, successVal);
      setShowDeathSaveDialog(false); setSuccess(successVal ? 'Death save SUCCESS!' : 'Death save FAILURE!'); setTimeout(() => setSuccess(null), 2000);
    } catch (e: any) { setError(e.message); }
  };

  const handleHeal = async () => {
    if (!activeCombat || !healTarget || healAmount <= 0) return;
    try {
      await invoke('CombatHeal', activeCombat.combatId, healTarget, healAmount);
      setShowHealDialog(false); setSuccess(`Healed ${healAmount} HP!`); setTimeout(() => setSuccess(null), 2000);
    } catch (e: any) { setError(e.message); }
  };

  const handleRemoveCondition = async (participantId: string, conditionName: string) => {
    if (!activeCombat) return;
    try { await invoke('CombatRemoveCondition', activeCombat.combatId, participantId, conditionName); }
    catch (e: any) { setError(e.message); }
  };

  const handleCastSpell = async () => {
    if (!activeCombat || !spellTarget || !spellName) return;
    try {
      await invoke('CombatCastSpell', activeCombat.combatId, user?.displayName || 'Player', spellName, spellTarget, spellSaveFormula, spellSaveDC, spellDamageFormula, spellDamageBonus, spellDescription);
      setShowSpellDialog(false); setSuccess(`Cast ${spellName}!`); setTimeout(() => setSuccess(null), 2000);
    } catch (e: any) { setError(e.message); }
  };

  const handleGetAISuggestions = async () => {
    if (!activeCombat) return;
    try {
      const suggestions = await invoke('CombatGetAISuggestions', activeCombat.combatId);
      if (suggestions) setAISuggestions(suggestions);
      setShowAISuggestions(true);
    } catch (e: any) { setError(e.message); }
  };

  const handleStartShortRest = async () => {
    if (!activeCombat) return;
    try { await invoke('CombatStartShortRest', activeCombat.combatId); setShowRestDialog(false); setSuccess('Short rest started!'); setTimeout(() => setSuccess(null), 2000); }
    catch (e: any) { setError(e.message); }
  };

  const handleStartLongRest = async () => {
    if (!activeCombat) return;
    try { await invoke('CombatStartLongRest', activeCombat.combatId); setShowRestDialog(false); setSuccess('Long rest started!'); setTimeout(() => setSuccess(null), 2000); }
    catch (e: any) { setError(e.message); }
  };

  const handleEndRest = async () => {
    if (!activeCombat) return;
    try { await invoke('CombatEndRest', activeCombat.combatId); setShowRestDialog(false); setSuccess('Rest ended!'); setTimeout(() => setSuccess(null), 2000); }
    catch (e: any) { setError(e.message); }
  };

  const handleSetGridSize = async () => {
    if (!activeCombat) return;
    try { await invoke('CombatSetGridSize', activeCombat.combatId, gridWidth, gridHeight); setShowGridDialog(false); setSuccess(`Grid set to ${gridWidth}x${gridHeight}`); setTimeout(() => setSuccess(null), 2000); }
    catch (e: any) { setError(e.message); }
  };

  const handleAddItem = async () => {
    if (!activeCombat || !invItemName || !sanTarget) return;
    try {
      await invoke('CombatAddItem', activeCombat.combatId, sanTarget, invItemName, invItemType, invQuantity);
      setShowInventoryDialog(false); setInvItemName(''); setSuccess(`Added ${invItemName}`); setTimeout(() => setSuccess(null), 2000);
    } catch (e: any) { setError(e.message); }
  };

  const handleSANLoss = async () => {
    if (!activeCombat || !sanTarget) return;
    try { await invoke('CombatApplySANLoss', activeCombat.combatId, sanTarget, sanLoss, 'Combat trauma'); setSuccess(`${sanLoss} SAN lost!`); setTimeout(() => setSuccess(null), 2000); loadActiveCombat(); }
    catch (e: any) { setError(e.message); }
  };

  const handleSANRecovery = async () => {
    if (!activeCombat || !sanTarget) return;
    try { await invoke('CombatApplySANRecovery', activeCombat.combatId, sanTarget, sanRecovery); setSuccess(`${sanRecovery} SAN recovered!`); setTimeout(() => setSuccess(null), 2000); loadActiveCombat(); }
    catch (e: any) { setError(e.message); }
  };

  const handleSANCheck = async () => {
    if (!activeCombat || !sanTarget) return;
    try { await invoke('CombatMakeSANCheck', activeCombat.combatId, sanTarget, sanDC); setShowSANDialog(false); }
    catch (e: any) { setError(e.message); }
  };

  const getCurrentTurnParticipant = () => {
    if (!activeCombat || activeCombat.participants.length === 0) return null;
    const idx = Math.min(activeCombat.currentTurnIndex, activeCombat.participants.length - 1);
    return activeCombat.participants[idx];
  };

  const sortedParticipants = activeCombat ? [...activeCombat.participants].sort((a, b) => b.initiative - a.initiative) : [];

  const getEventIcon = (type: string) => {
    switch (type) {
      case 'CombatStart': return '🎯'; case 'CombatEnd': return '🏁'; case 'TurnChange': return '🔄';
      case 'Attack': return '⚔️'; case 'Damage': return '💥'; case 'Healing': return '💚';
      case 'Condition': return '🔮'; case 'SaveThrow': return '🛡️'; case 'Initiative': return '🎲';
      case 'Death': return '💀'; case 'Revival': return '✨'; case 'RoundStart': return '📢';
      default: return '•';
    }
  };

  const getEventColor = (type: string) => {
    switch (type) {
      case 'Damage': return 'error.main'; case 'Healing': return 'success.main';
      case 'Death': return '#8B0000'; case 'Revival': return '#FFD700';
      case 'Condition': return 'primary.main'; case 'Attack': return 'warning.main';
      default: return 'text.secondary';
    }
  };

  if (!activeCombat || activeCombat.participants.length === 0) {
    return (
      <Paper sx={{ p: 3, textAlign: 'center' }}>
        <Typography variant="h6" gutterBottom sx={{ color: 'text.secondary' }}>
          <CombatIcon sx={{ mr: 1, verticalAlign: 'middle' }} /> No Active Combat
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>Start a combat encounter to track turns, initiative, and combat actions.</Typography>
        <Button variant="contained" color="error" startIcon={<CombatIcon />} onClick={handleStartCombat} size="large">Start Combat</Button>
      </Paper>
    );
  }

  const currentTurn = getCurrentTurnParticipant();

  return (
    <>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CombatIcon color="error" fontSize="large" />
          <Typography variant="h5">{activeCombat.name || 'Combat'}</Typography>
          <Chip label={activeCombat.status} size="small" color={activeCombat.status === 'Active' ? 'success' : 'default'} />
          <Chip label={`Round ${activeCombat.currentRound}`} size="small" variant="outlined" />
        </Box>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          {activeCombat.status === 'Active' && (
            <Button size="small" variant="outlined" startIcon={<PauseIcon />} onClick={handlePauseCombat}>Pause</Button>
          )}
          {activeCombat.status === 'Paused' && (
            <Button size="small" variant="outlined" color="success" startIcon={<PlayIcon />} onClick={handleResumeCombat}>Resume</Button>
          )}
          <Button size="small" variant="outlined" startIcon={<RefreshIcon />} onClick={handleEndCombat} color="error">End</Button>
        </Box>
      </Box>

      {error && (<Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>{error}</Alert>)}
      {success && (<Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2 }}>{success}</Alert>)}

      {/* Current Turn Banner */}
      <Paper sx={{ p: 2, mb: 2, bgcolor: currentTurn?.isDead ? '#3d0000' : 'primary.main', color: 'white', display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
        <Typography variant="h6">{currentTurn?.isDead ? '💀 ' : '👑 '}{currentTurn?.displayName || 'Unknown'}'s Turn</Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          <Chip icon={<HPIcon />} label={`HP: ${currentTurn?.currentHP ?? '?'} / ${currentTurn?.maxHP ?? '?'}`} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />
          <Chip icon={<ACIcon />} label={`AC: ${currentTurn?.ac ?? '?'}`} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />
          <Chip icon={<InitiativeIcon />} label={`Init: ${currentTurn?.initiative ?? '?'}`} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />
          {currentTurn?.conditions && currentTurn.conditions.length > 0 && (
            <Chip icon={<ConditionIcon />} label={`${currentTurn.conditions.length} condition${currentTurn.conditions.length > 1 ? 's' : ''}`} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />
          )}
        </Box>
      </Paper>

      {/* Action Bar */}
      <Paper sx={{ p: 1, mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
        <Button size="small" variant="outlined" startIcon={<TurnIcon />} onClick={handleAdvanceTurn}>Next Turn</Button>
        <Button size="small" variant="outlined" startIcon={<InitiativeIcon />} onClick={() => setShowRollInitiative(true)}>🎲 Roll Initiative</Button>
        <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={() => setShowAddParticipant(true)}>Add Participant</Button>
        <Divider sx={{ height: 24, mx: 0.5 }} orientation="vertical" />
        <Button size="small" variant="outlined" startIcon={<AttackIcon />} onClick={() => setShowAttackDialog(true)}>Attack</Button>
        <Button size="small" variant="outlined" startIcon={<SpellIcon />} onClick={() => setShowSpellDialog(true)}>Cast Spell</Button>
        <Button size="small" variant="outlined" startIcon={<HealIcon />} onClick={() => setShowHealDialog(true)}>Heal</Button>
        <Button size="small" variant="outlined" startIcon={<ConditionIcon />} onClick={() => setShowConditionDialog(true)}>Condition</Button>
        <Button size="small" variant="outlined" startIcon={<DeathSaveIcon />} onClick={() => setShowDeathSaveDialog(true)}>Death Save</Button>
        <Button size="small" variant="outlined" startIcon={<ACIcon />} onClick={() => setShowSaveThrowDialog(true)}>Save Throw</Button>
        <Divider sx={{ height: 24, mx: 0.5 }} orientation="vertical" />
        <Button size="small" variant="outlined" startIcon={<AICogIcon />} onClick={handleGetAISuggestions}>🤖 AI Suggestions</Button>
        <Button size="small" variant="outlined" startIcon={<RestIcon />} onClick={() => setShowRestDialog(true)}>Rest</Button>
        <Button size="small" variant="outlined" startIcon={<GridIcon />} onClick={() => setShowGridDialog(true)}>Grid</Button>
        <Button size="small" variant="outlined" startIcon={<InventoryIcon />} onClick={() => setShowInventoryDialog(true)}>Inventory</Button>
        <Button size="small" variant="outlined" startIcon={<StarIcon />} onClick={() => setShowSANDialog(true)}>SAN</Button>
        <Box sx={{ flex: 1 }} />
        <IconButton size="small" onClick={() => setShowCombatLog(!showCombatLog)}>
          {showCombatLog ? <ExpandLessIcon /> : <ExpandMoreIcon />}
          <HistoryIcon fontSize="small" sx={{ ml: 0.5 }} />
        </IconButton>
      </Paper>

      <Grid container spacing={2}>
        {/* Initiative Tracker */}
        <Grid size={{ xs: 12, lg: 7 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <PeopleIcon /> Initiative Tracker ({activeCombat.participants.length})
            </Typography>
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Order</TableCell><TableCell>Participant</TableCell><TableCell>HP</TableCell>
                    <TableCell>AC</TableCell><TableCell>Init</TableCell><TableCell>Conditions</TableCell>
                    <TableCell sx={{ textAlign: 'right' }}>Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {sortedParticipants.map((p: CombatParticipantSummary, idx: number) => (
                    <TableRow key={p.id} sx={{ bgcolor: p.isCurrentTurn ? 'primary.light' : 'inherit', opacity: p.isDead ? 0.5 : 1 }}>
                      <TableCell><Chip label={idx + 1} size="small" color={p.isCurrentTurn ? 'primary' : 'default'} variant={p.isCurrentTurn ? 'filled' : 'outlined'} /></TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                          <Avatar sx={{ width: 20, height: 20, fontSize: 10, bgcolor: p.participantType === 'NPC' ? 'error.main' : 'primary.main' }}>
                            {p.participantType === 'NPC' ? '👹' : '👤'}
                          </Avatar>
                          <Typography variant="body2" sx={{ fontWeight: p.isCurrentTurn ? 'bold' : 'normal' }}>{p.displayName}</Typography>
                        </Box>
                      </TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                          <HPIcon fontSize="small" sx={{ color: p.currentHP < p.maxHP * 0.3 ? 'error.main' : 'success.main' }} />
                          <Typography variant="body2" sx={{ color: p.currentHP < p.maxHP * 0.3 ? 'error.main' : 'inherit' }}>{p.currentHP}/{p.maxHP}</Typography>
                        </Box>
                      </TableCell>
                      <TableCell><Typography variant="body2">{p.ac}</Typography></TableCell>
                      <TableCell><Typography variant="body2">{p.initiative}</Typography></TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', gap: 0.25, flexWrap: 'wrap' }}>
                          {p.conditions.map((c: ConditionEntry, ci: number) => (
                            <Tooltip key={ci} title={`${c.name}${c.duration > 0 ? ` (${c.duration} rounds)` : ''}`}>
                              <Chip label={c.name} size="small" color="warning" variant="outlined" onDelete={c.duration <= 0 ? undefined : () => handleRemoveCondition(p.id, c.name)} />
                            </Tooltip>
                          ))}
                        </Box>
                      </TableCell>
                      <TableCell sx={{ textAlign: 'right' }}>
                        <Box sx={{ display: 'flex', gap: 0.25, justifyContent: 'flex-end' }}>
                          <Tooltip title="Attack"><IconButton size="small" onClick={() => { setAttackTarget(p.id); setShowAttackDialog(true); }}><AttackIcon fontSize="small" /></IconButton></Tooltip>
                          <Tooltip title="Cast Spell"><IconButton size="small" onClick={() => { setSpellTarget(p.id); setShowSpellDialog(true); }}><SpellIcon fontSize="small" /></IconButton></Tooltip>
                          <Tooltip title="Heal"><IconButton size="small" onClick={() => { setHealTarget(p.id); setShowHealDialog(true); }}><HealIcon fontSize="small" /></IconButton></Tooltip>
                          {p.currentHP <= 0 && (<Tooltip title="Death Save"><IconButton size="small" onClick={() => { setDeathSaveTarget(p.id); setShowDeathSaveDialog(true); }}><DeathSaveIcon fontSize="small" color="error" /></IconButton></Tooltip>)}
                          <Tooltip title="Remove"><IconButton size="small" onClick={() => handleRemoveParticipant(p.id)} color="error"><RemoveIcon fontSize="small" /></IconButton></Tooltip>
                        </Box>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>
        </Grid>

        {/* Combat Log */}
        <Grid size={{ xs: 12, lg: 5 }}>
          <Collapse in={showCombatLog}>
            <Paper sx={{ p: 2, maxHeight: '70vh', overflow: 'auto' }}>
              <Typography variant="h6" gutterBottom sx={{ display: 'flex', alignItems: 'center', gap: 1 }}><HistoryIcon /> Combat Log</Typography>
              {activeCombat.events.length === 0 ? (
                <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 4 }}>No combat events yet.</Typography>
              ) : (
                <Box>
                  {activeCombat.events.map((evt: CombatLogEvent) => (
                    <Box key={evt.id} sx={{ mb: 0.5, pl: 1, borderLeft: `2px solid ${getEventColor(evt.type)}` }}>
                      <Typography variant="caption" color="text.secondary">R{evt.round} T{evt.turnIndex} · {new Date(evt.createdAt).toLocaleTimeString()}</Typography>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        <Typography variant="body2" sx={{ color: getEventColor(evt.type) }}>{getEventIcon(evt.type)}</Typography>
                        <Typography variant="body2"><strong>{evt.actorName}</strong>{evt.targetName && ` → ${evt.targetName}`}{': '}{evt.content}</Typography>
                      </Box>
                    </Box>
                  ))}
                </Box>
              )}
            </Paper>
          </Collapse>
        </Grid>
      </Grid>

      {/* ==================== Dialogs ==================== */}

      <Dialog open={showRollInitiative} onClose={() => setShowRollInitiative(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Roll Initiative</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>All participants will roll 1d20 for initiative.</Typography>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {sortedParticipants.map((p: CombatParticipantSummary) => (<Chip key={p.id} label={`${p.displayName} (${p.initiative})`} size="small" />))}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowRollInitiative(false)}>Cancel</Button>
          <Button onClick={handleRollInitiative} variant="contained" color="warning" startIcon={<InitiativeIcon />}>Roll Initiative</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showAddParticipant} onClose={() => setShowAddParticipant(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Participant</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <FormControl fullWidth>
            <InputLabel>Type</InputLabel>
            <Select value={newParticipantType} label="Type" onChange={e => setNewParticipantType(e.target.value)}>
              <MenuItem value="NPC">NPC / Monster</MenuItem>
              <MenuItem value="Player">Player Character</MenuItem>
            </Select>
          </FormControl>
          <TextField fullWidth label="Display Name" value={newDisplayName} onChange={e => setNewDisplayName(e.target.value)} autoFocus />
          <Grid container spacing={2}>
            <Grid size={{ xs: 4 }}><TextField fullWidth label="AC" type="number" value={newAC} onChange={e => setNewAC(parseInt(e.target.value) || 10)} /></Grid>
            <Grid size={{ xs: 4 }}><TextField fullWidth label="Current HP" type="number" value={newHP} onChange={e => setNewHP(parseInt(e.target.value) || 10)} /></Grid>
            <Grid size={{ xs: 4 }}><TextField fullWidth label="Max HP" type="number" value={newMaxHP} onChange={e => setNewMaxHP(parseInt(e.target.value) || 10)} /></Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowAddParticipant(false)}>Cancel</Button>
          <Button onClick={handleAddParticipant} variant="contained" startIcon={<AddIcon />}>Add</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showAttackDialog} onClose={() => setShowAttackDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Attack</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <FormControl fullWidth>
            <InputLabel>Target</InputLabel>
            <Select value={attackTarget} label="Target" onChange={e => setAttackTarget(e.target.value)}>
              {sortedParticipants.filter(p => !p.isDead).map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName} ({p.currentHP}/{p.maxHP} HP, AC {p.ac})</MenuItem>))}
            </Select>
          </FormControl>
          <TextField fullWidth label="Weapon" value={weaponName} onChange={e => setWeaponName(e.target.value)} />
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Attack Roll" value={attackFormula} onChange={e => setAttackFormula(e.target.value)} /></Grid>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Attack Bonus" type="number" value={attackBonus} onChange={e => setAttackBonus(parseInt(e.target.value) || 0)} /></Grid>
          </Grid>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Damage Formula" value={damageFormula} onChange={e => setDamageFormula(e.target.value)} /></Grid>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Damage Bonus" type="number" value={damageBonus} onChange={e => setDamageBonus(parseInt(e.target.value) || 0)} /></Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowAttackDialog(false)}>Cancel</Button>
          <Button onClick={handleAttack} variant="contained" color="error" startIcon={<AttackIcon />}>Attack</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showSpellDialog} onClose={() => setShowSpellDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Cast Spell</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <FormControl fullWidth>
            <InputLabel>Target</InputLabel>
            <Select value={spellTarget} label="Target" onChange={e => setSpellTarget(e.target.value)}>
              {sortedParticipants.filter(p => !p.isDead).map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName} ({p.currentHP}/{p.maxHP} HP, AC {p.ac})</MenuItem>))}
            </Select>
          </FormControl>
          <TextField fullWidth label="Spell Name" value={spellName} onChange={e => setSpellName(e.target.value)} autoFocus />
          <FormControl fullWidth>
            <InputLabel>Spell Level</InputLabel>
            <Select value={spellLevel} label="Spell Level" onChange={e => setSpellLevel(e.target.value)}>
              <MenuItem value="0">Cantrip</MenuItem>
              <MenuItem value="1">1st Level</MenuItem>
              <MenuItem value="2">2nd Level</MenuItem>
              <MenuItem value="3">3rd Level</MenuItem>
              <MenuItem value="4">4th Level</MenuItem>
              <MenuItem value="5">5th Level</MenuItem>
              <MenuItem value="6">6th Level</MenuItem>
              <MenuItem value="7">7th Level</MenuItem>
              <MenuItem value="8">8th Level</MenuItem>
              <MenuItem value="9">9th Level</MenuItem>
            </Select>
          </FormControl>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Save Formula" value={spellSaveFormula} onChange={e => setSpellSaveFormula(e.target.value)} /></Grid>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Save DC" type="number" value={spellSaveDC} onChange={e => setSpellSaveDC(parseInt(e.target.value) || 15)} /></Grid>
          </Grid>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Damage Formula" value={spellDamageFormula} onChange={e => setSpellDamageFormula(e.target.value)} /></Grid>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Damage Bonus" type="number" value={spellDamageBonus} onChange={e => setSpellDamageBonus(parseInt(e.target.value) || 0)} /></Grid>
          </Grid>
          <TextField fullWidth label="Description (optional)" value={spellDescription} onChange={e => setSpellDescription(e.target.value)} multiline rows={2} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowSpellDialog(false)}>Cancel</Button>
          <Button onClick={handleCastSpell} variant="contained" color="primary" startIcon={<SpellIcon />}>Cast Spell</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showSaveThrowDialog} onClose={() => setShowSaveThrowDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Save Throw</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <FormControl fullWidth>
            <InputLabel>Target</InputLabel>
            <Select value={saveTarget} label="Target" onChange={e => setSaveTarget(e.target.value)}>
              {sortedParticipants.map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName}</MenuItem>))}
            </Select>
          </FormControl>
          <FormControl fullWidth>
            <InputLabel>Save Type</InputLabel>
            <Select value={saveType} label="Save Type" onChange={e => setSaveType(e.target.value)}>
              <MenuItem value="Fortitude">Fortitude</MenuItem>
              <MenuItem value="Reflex">Reflex</MenuItem>
              <MenuItem value="Will">Will</MenuItem>
              <MenuItem value="Constitution">Constitution</MenuItem>
              <MenuItem value="Dexterity">Dexterity</MenuItem>
              <MenuItem value="Strength">Strength</MenuItem>
              <MenuItem value="Intelligence">Intelligence</MenuItem>
              <MenuItem value="Wisdom">Wisdom</MenuItem>
              <MenuItem value="Charisma">Charisma</MenuItem>
            </Select>
          </FormControl>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Save Formula" value={saveFormula} onChange={e => setSaveFormula(e.target.value)} /></Grid>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="DC" type="number" value={saveDC} onChange={e => setSaveDC(parseInt(e.target.value) || 15)} /></Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowSaveThrowDialog(false)}>Cancel</Button>
          <Button onClick={handleSaveThrow} variant="contained" color="info" startIcon={<ACIcon />}>Roll Save</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showConditionDialog} onClose={() => setShowConditionDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Apply Condition</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <FormControl fullWidth>
            <InputLabel>Target</InputLabel>
            <Select value={conditionTarget} label="Target" onChange={e => setConditionTarget(e.target.value)}>
              {sortedParticipants.map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName}</MenuItem>))}
            </Select>
          </FormControl>
          <TextField fullWidth label="Condition Name" value={conditionName} onChange={e => setConditionName(e.target.value)} placeholder="e.g., Poisoned, Grappled, Frightened" autoFocus />
          <Box>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>Quick conditions:</Typography>
            <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
              {['Blinded', 'Deafened', 'Frightened', 'Grappled', 'Paralyzed', 'Petrified', 'Poisoned', 'Prone', 'Restrained', 'Stunned', 'Unconscious'].map(c => (
                <Chip key={c} label={c} size="small" clickable onClick={() => setConditionName(c)} />
              ))}
            </Box>
          </Box>
          <TextField fullWidth label="Duration (rounds, 0 = indefinite)" type="number" value={conditionDuration} onChange={e => setConditionDuration(parseInt(e.target.value) || 0)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowConditionDialog(false)}>Cancel</Button>
          <Button onClick={handleApplyCondition} variant="contained" color="warning" startIcon={<ConditionIcon />}>Apply</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showDeathSaveDialog} onClose={() => setShowDeathSaveDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Death Saving Throw</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <FormControl fullWidth>
            <InputLabel>Target</InputLabel>
            <Select value={deathSaveTarget} label="Target" onChange={e => setDeathSaveTarget(e.target.value)}>
              {sortedParticipants.filter(p => p.currentHP <= 0).map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName} (0 HP)</MenuItem>))}
            </Select>
          </FormControl>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowDeathSaveDialog(false)}>Cancel</Button>
          <Button onClick={() => handleDeathSave(false)} variant="outlined" color="error" startIcon={<ConditionIcon />}>Failed</Button>
          <Button onClick={() => handleDeathSave(true)} variant="contained" color="success" startIcon={<HPIcon />}>Success</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showHealDialog} onClose={() => setShowHealDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Heal</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <FormControl fullWidth>
            <InputLabel>Target</InputLabel>
            <Select value={healTarget} label="Target" onChange={e => setHealTarget(e.target.value)}>
              {sortedParticipants.filter(p => p.currentHP < p.maxHP).map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName} ({p.currentHP}/{p.maxHP} HP)</MenuItem>))}
            </Select>
          </FormControl>
          <TextField fullWidth label="Heal Amount" type="number" value={healAmount} onChange={e => setHealAmount(parseInt(e.target.value) || 1)} inputProps={{ min: 1 }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowHealDialog(false)}>Cancel</Button>
          <Button onClick={handleHeal} variant="contained" color="success" startIcon={<HealIcon />}>Heal {healAmount} HP</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showAISuggestions} onClose={() => setShowAISuggestions(false)} maxWidth="md" fullWidth>
        <DialogTitle>🤖 AI Combat Suggestions</DialogTitle>
        <DialogContent sx={{ mt: 1, maxHeight: '70vh', overflow: 'auto' }}>
          {aiSuggestions ? (
            <>
              <Box sx={{ mb: 2 }}>
                <Chip label={`Threat Level: ${aiSuggestions.threatLevel}`} color={
                  aiSuggestions.threatLevel === 'Extreme' ? 'error' : aiSuggestions.threatLevel === 'High' ? 'warning' : aiSuggestions.threatLevel === 'Medium' ? 'info' : 'success'
                } sx={{ mr: 1, mb: 1, fontSize: '1rem', height: 32 }} />
                <Typography variant="body2" sx={{ mt: 1 }}><strong>Strategy:</strong> {aiSuggestions.recommendedStrategy}</Typography>
              </Box>
              <Divider sx={{ my: 2 }} />
              <Typography variant="h6" gutterBottom>Player Actions</Typography>
              {aiSuggestions.suggestions.map((s, i) => (
                <Paper key={i} variant="outlined" sx={{ p: 1, mb: 1 }}>
                  <Typography variant="body2"><strong>{s.actor}</strong> → {s.action} on <strong>{s.target || 'target'}</strong></Typography>
                  <Typography variant="caption" color="text.secondary">Reason: {s.reason}</Typography>
                </Paper>
              ))}
              <Divider sx={{ my: 2 }} />
              <Typography variant="h6" gutterBottom>NPC Actions</Typography>
              {aiSuggestions.npcActions.map((npc, i) => (
                <Paper key={i} variant="outlined" sx={{ p: 1, mb: 1 }}>
                  <Typography variant="body2"><strong>{npc.npcName}</strong> ({npc.behavior}) → {npc.action} on <strong>{npc.target}</strong></Typography>
                  <Typography variant="caption" color="text.secondary">Reason: {npc.reason}</Typography>
                </Paper>
              ))}
              {aiSuggestions.warnings.length > 0 && (
                <>
                  <Divider sx={{ my: 2 }} />
                  <Typography variant="h6" gutterBottom>Warnings</Typography>
                  {aiSuggestions.warnings.map((w, i) => (
                    <Chip key={i} label={w.message} color={w.severity === 'High' ? 'error' : w.severity === 'Medium' ? 'warning' : 'default'} size="small" sx={{ mr: 0.5, mb: 0.5 }} />
                  ))}
                </>
              )}
            </>
          ) : (<Typography color="text.secondary">Loading suggestions...</Typography>)}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowAISuggestions(false)}>Close</Button>
          <Button onClick={handleGetAISuggestions} variant="outlined" startIcon={<RefreshIcon />}>Refresh</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showRestDialog} onClose={() => setShowRestDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Rest</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>Take a rest to recover HP and resources.</Typography>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <Button variant="outlined" onClick={handleStartShortRest}>🛏️ Short Rest (recover HP & resources)</Button>
            <Button variant="outlined" onClick={handleStartLongRest}>🌙 Long Rest (full recovery)</Button>
            <Button variant="outlined" color="error" onClick={handleEndRest}>⏹️ End Rest</Button>
          </Box>
        </DialogContent>
        <DialogActions><Button onClick={() => setShowRestDialog(false)}>Close</Button></DialogActions>
      </Dialog>

      <Dialog open={showGridDialog} onClose={() => setShowGridDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Grid/Map</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography variant="body2" color="text.secondary">Set grid size for tactical combat.</Typography>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Width" type="number" value={gridWidth} onChange={e => setGridWidth(parseInt(e.target.value) || 20)} /></Grid>
            <Grid size={{ xs: 6 }}><TextField fullWidth label="Height" type="number" value={gridHeight} onChange={e => setGridHeight(parseInt(e.target.value) || 15)} /></Grid>
          </Grid>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {[10, 15, 20, 25, 30, 40, 50].map(size => (<Chip key={size} label={`${size}x${size}`} size="small" clickable onClick={() => { setGridWidth(size); setGridHeight(size); }} />))}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowGridDialog(false)}>Close</Button>
          <Button onClick={handleSetGridSize} variant="contained" startIcon={<GridIcon />}>Set Grid</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showInventoryDialog} onClose={() => setShowInventoryDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Inventory</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography variant="body2" color="text.secondary">Add items to a participant's inventory.</Typography>
          <FormControl fullWidth>
            <InputLabel>Target</InputLabel>
            <Select value={sanTarget} label="Target" onChange={e => setSanTarget(e.target.value)}>
              {sortedParticipants.map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName}</MenuItem>))}
            </Select>
          </FormControl>
          <TextField fullWidth label="Item Name" value={invItemName} onChange={e => setInvItemName(e.target.value)} autoFocus />
          <FormControl fullWidth>
            <InputLabel>Item Type</InputLabel>
            <Select value={invItemType} label="Item Type" onChange={e => setInvItemType(e.target.value)}>
              <MenuItem value="Weapon">Weapon</MenuItem>
              <MenuItem value="Armor">Armor</MenuItem>
              <MenuItem value="Shield">Shield</MenuItem>
              <MenuItem value="Item">Item</MenuItem>
              <MenuItem value="Consumable">Consumable</MenuItem>
              <MenuItem value="Magic">Magic Item</MenuItem>
            </Select>
          </FormControl>
          <TextField fullWidth label="Quantity" type="number" value={invQuantity} onChange={e => setInvQuantity(parseInt(e.target.value) || 1)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowInventoryDialog(false)}>Close</Button>
          <Button onClick={handleAddItem} variant="contained" startIcon={<AddIcon />}>Add Item</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={showSANDialog} onClose={() => setShowSANDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Sanity (CoC 7e)</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography variant="body2" color="text.secondary">Manage SAN (Sanity) for Call of Cthulhu characters.</Typography>
          <FormControl fullWidth>
            <InputLabel>Target</InputLabel>
            <Select value={sanTarget} label="Target" onChange={e => setSanTarget(e.target.value)}>
              {sortedParticipants.map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName}</MenuItem>))}
            </Select>
          </FormControl>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <TextField fullWidth label="SAN Loss Amount" type="number" value={sanLoss} onChange={e => setSanLoss(parseInt(e.target.value) || 1)} />
            <Button variant="outlined" color="error" onClick={handleSANLoss}>Apply SAN Loss</Button>
          </Box>
          <Divider />
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <TextField fullWidth label="SAN Recovery Amount" type="number" value={sanRecovery} onChange={e => setSanRecovery(parseInt(e.target.value) || 1)} />
            <Button variant="outlined" color="success" onClick={handleSANRecovery}>Apply SAN Recovery</Button>
          </Box>
          <Divider />
          <Typography variant="subtitle2">SAN Check</Typography>
          <Grid container spacing={2}>
            <Grid size={{ xs: 8 }}><TextField fullWidth label="DC" type="number" value={sanDC} onChange={e => setSanDC(parseInt(e.target.value) || 20)} /></Grid>
            <Grid size={{ xs: 4 }}><Button variant="contained" fullWidth onClick={handleSANCheck}>Roll</Button></Grid>
          </Grid>
        </DialogContent>
        <DialogActions><Button onClick={() => setShowSANDialog(false)}>Close</Button></DialogActions>
      </Dialog>
    </>
  );
}
