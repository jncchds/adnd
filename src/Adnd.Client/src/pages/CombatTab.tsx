import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams } from 'react-router-dom';
import { useAuth } from '../api/hooks/useAuth';
import { useGameHub } from '../api/hooks/useHub';
import {
  Box, Typography, Paper, Button, IconButton,
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Chip, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Alert, Collapse, MenuItem, Select,
  FormControl, InputLabel, Grid, Divider, Avatar,
  Tooltip, List, ListItem, ListItemText, ListItemAvatar,
  Slider,
} from '@mui/material';
import {
  DirectionsRun as CombatIcon, Replay as TurnIcon, People as PeopleIcon,
  Add as AddIcon, Remove as RemoveIcon, HealthAndSafety as HPIcon,
  Shield as ACIcon, EmojiEvents as InitiativeIcon,
  ExpandMore as ExpandMoreIcon,
  Gavel as AttackIcon, LocalHospital as HealIcon,
  Warning as ConditionIcon, AutoFixNormal as DeathSaveIcon,
  Refresh as RefreshIcon, Pause as PauseIcon, PlayArrow as PlayIcon,
  History as HistoryIcon, AutoAwesome as SpellIcon,
  Psychology as AICogIcon, Bed as RestIcon,
  Map as GridIcon, Inventory as InventoryIcon,
  Star as StarIcon,
  Book as SheetIcon,
  CheckCircle as CheckCircleIcon,
  Cancel as CancelIcon,
  Delete as DeleteIcon,
} from '@mui/icons-material';
import {
  type CombatLog, type CombatParticipantSummary,
  type ConditionEntry, type CombatLogEvent,
  type SpellCastResult, type AISuggestions, type SANCheckResult,
} from '../types';

// ==================== Sub-components ====================

function CombatLogPanel({ events, showCombatLog }: {
  events: CombatLogEvent[]; showCombatLog: boolean;
}) {
  const logRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (showCombatLog && logRef.current) {
      logRef.current.scrollTop = logRef.current.scrollHeight;
    }
  }, [events, showCombatLog]);

  const getEventIcon = (type: string) => {
    switch (type) {
      case 'CombatStart': return '🎯'; case 'CombatEnd': return '🏁'; case 'TurnChange': return '🔄';
      case 'Attack': return '⚔️'; case 'Damage': return '💥'; case 'Healing': return '💚';
      case 'Condition': return '🔮'; case 'SaveThrow': return '🛡️'; case 'Initiative': return '🎲';
      case 'Death': return '💀'; case 'Revival': return '✨'; case 'RoundStart': return '📢';
      case 'DeathSave': return '☠️';
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

  const groupByRound = (evts: CombatLogEvent[]) => {
    const groups: Record<number, CombatLogEvent[]> = {};
    evts.forEach(e => {
      if (!groups[e.round]) groups[e.round] = [];
      groups[e.round].push(e);
    });
    return Object.entries(groups).sort(([a], [b]) => Number(a) - Number(b));
  };

  const grouped = groupByRound(events);

  return (
    <Paper ref={logRef} sx={{ maxHeight: '70vh', overflow: 'auto' }}>
      <Box sx={{ p: 1.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center', position: 'sticky', top: 0, bgcolor: 'background.paper', zIndex: 1, borderBottom: 1, borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <HistoryIcon fontSize="small" /> Combat Log
        </Typography>
        <Chip label={`${events.length} events`} size="small" variant="outlined" />
      </Box>
      <Divider />
      {events.length === 0 ? (
        <Box sx={{ p: 4, textAlign: 'center' }}>
          <Typography variant="body2" color="text.secondary">No combat events yet. Start a combat to begin tracking.</Typography>
        </Box>
      ) : (
        <Box>
          {grouped.map(([round, evts]) => (
            <Box key={round}>
              <Box sx={{ p: 0.5, px: 2, bgcolor: 'action.hover', position: 'sticky', top: 36, zIndex: 1 }}>
                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 'bold' }}>
                  Round {round}
                </Typography>
              </Box>
              {evts.map((evt) => (
                <Box key={evt.id} sx={{
                  p: 0.75, px: 2,
                  borderLeft: `3px solid ${getEventColor(evt.type)}`,
                  ml: 1, mr: 1, mb: 0.25,
                  bgcolor: evt.type === 'Death' ? 'rgba(139,0,0,0.05)' : evt.type === 'Healing' ? 'rgba(76,175,80,0.05)' : 'transparent',
                }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.25 }}>
                    <Typography variant="caption" color="text.secondary">
                      {getEventIcon(evt.type)}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      T{evt.turnIndex} · {new Date(evt.createdAt).toLocaleTimeString()}
                    </Typography>
                  </Box>
                  <Typography variant="body2">
                    <strong>{evt.actorName}</strong>
                    {evt.targetName && <span> → <em>{evt.targetName}</em></span>}
                    : {evt.content}
                  </Typography>
                </Box>
              ))}
            </Box>
          ))}
        </Box>
      )}
    </Paper>
  );
}

function DeathSaveTracker({ participant }: { participant: CombatParticipantSummary }) {
  const deathState = (participant as any).deathSaveState as { successes?: number; failures?: number } | null;
  const successes = deathState?.successes ?? 0;
  const failures = deathState?.failures ?? 0;

  if (participant.currentHP > 0) return null;

  return (
    <Box sx={{ mt: 0.5 }}>
      <Typography variant="caption" color="text.secondary">Death Saves:</Typography>
      <Box sx={{ display: 'flex', gap: 0.5, mt: 0.25 }}>
        {[0, 1, 2].map(i => (
          <Box key={`s${i}`} sx={{
            width: 14, height: 14, borderRadius: '50%',
            bgcolor: i < successes ? 'success.main' : 'grey.500',
            border: `2px solid ${i < successes ? 'success.main' : 'grey.400'}`,
          }} />
        ))}
        <Typography variant="caption" color="text.secondary" sx={{ mx: 0.5 }}>|</Typography>
        {[0, 1, 2].map(i => (
          <Box key={`f${i}`} sx={{
            width: 14, height: 14, borderRadius: '50%',
            bgcolor: i < failures ? 'error.main' : 'grey.500',
            border: `2px solid ${i < failures ? 'error.main' : 'grey.400'}`,
          }} />
        ))}
      </Box>
    </Box>
  );
}

// ==================== Action Economy Tracker ====================

interface ActionEconomyTrackerProps {
  participant: CombatParticipantSummary;
  actionsRemaining?: number;
  bonusActionsRemaining?: number;
  reactionsRemaining?: number;
  movementsRemaining?: number;
  onSpendAction?: () => void;
  onSpendBonusAction?: () => void;
  onSpendReaction?: () => void;
  onSpendMovement?: () => void;
  onRefresh?: () => void;
  isEditable?: boolean;
}

function ActionEconomyTracker({
  participant: _participant, actionsRemaining, bonusActionsRemaining, reactionsRemaining, movementsRemaining,
  onSpendAction, onSpendBonusAction, onSpendReaction, onSpendMovement, onRefresh, isEditable = false,
}: ActionEconomyTrackerProps) {
  const a = actionsRemaining ?? 1;
  const b = bonusActionsRemaining ?? 0;
  const r = reactionsRemaining ?? 1;
  const m = movementsRemaining ?? 1;

  const ActionChip = ({ count, max, label, color, onClick }: {
    count: number; max: number; label: string; color: string; onClick?: () => void;
  }) => (
    <Box sx={{
      display: 'flex', alignItems: 'center', gap: 0.25,
      bgcolor: count === 0 ? 'rgba(244,67,54,0.1)' : 'rgba(0,0,0,0.05)',
      border: '1px solid',
      borderColor: count === 0 ? 'error.light' : `${color}.light`,
      borderRadius: 1,
      px: 0.75,
      py: 0.25,
      cursor: onClick && isEditable ? 'pointer' : 'default',
      opacity: onClick && isEditable ? 1 : 0.7,
      transition: 'all 0.15s',
      '&:hover': onClick && isEditable ? { bgcolor: `${color}.lighter` } : {},
    }}
      onClick={onClick}
    >
      <Typography variant="caption" sx={{ color: count === 0 ? 'error.main' : `${color}.main`, fontWeight: 'bold' }}>
        {count}
      </Typography>
      <Typography variant="caption" color="text.secondary">/</Typography>
      <Typography variant="caption" color="text.secondary">{max}</Typography>
      <Typography variant="caption" color="text.secondary" sx={{ ml: 0.25 }}>{label}</Typography>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', alignItems: 'center' }}>
      <ActionChip count={a} max={1} label="A" color="primary" onClick={isEditable && onSpendAction ? onSpendAction : undefined} />
      {b > 0 && <ActionChip count={b} max={b} label="BA" color="warning" onClick={isEditable && onSpendBonusAction ? onSpendBonusAction : undefined} />}
      <ActionChip count={r} max={1} label="R" color="info" onClick={isEditable && onSpendReaction ? onSpendReaction : undefined} />
      <ActionChip count={m} max={1} label="M" color="success" onClick={isEditable && onSpendMovement ? onSpendMovement : undefined} />
      {isEditable && onRefresh && (
        <Tooltip title="Refresh actions">
          <IconButton size="small" onClick={onRefresh} sx={{ color: 'text.secondary' }}>
            <RefreshIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      )}
    </Box>
  );
}

// ==================== Condition Manager ====================

interface ConditionManagerProps {
  participant: CombatParticipantSummary | null;
  open: boolean;
  onClose: () => void;
  onRemoveCondition: (participantId: string, conditionName: string) => void;
  onAddCondition: (participantId: string, conditionName: string, duration: number, description?: string) => void;
  isEditable: boolean;
  onCloseDialog?: () => void;
}

function ConditionManager({ participant, open, onClose, onRemoveCondition, onAddCondition, isEditable, onCloseDialog }: ConditionManagerProps) {
  const [newConditionName, setNewConditionName] = useState('');
  const [newConditionDuration, setNewConditionDuration] = useState(1);
  const [newConditionDesc, setNewConditionDesc] = useState('');

  if (!participant) return null;

  const commonConditions = [
    'Blinded', 'Deafened', 'Frightened', 'Grappled', 'Paralyzed', 'Petrified',
    'Poisoned', 'Prone', 'Restrained', 'Stunned', 'Unconscious', 'Invisible',
    'Ensnared', 'Exhaustion', 'Charmed', 'Confused', 'Incapacitated',
  ];

  const getConditionColor = (name: string): string => {
    const colors: Record<string, string> = {
      Blinded: 'grey', Deafened: 'grey', Frightened: 'warning', Grappled: 'warning',
      Paralyzed: 'error', Petrified: 'grey', Poisoned: 'success', Prone: 'default',
      Restrained: 'warning', Stunned: 'error', Unconscious: 'grey', Invisible: 'info',
      Ensnared: 'warning', Charmed: 'info', Confused: 'warning', Incapacitated: 'error',
    };
    return colors[name] || 'default';
  };

  const getConditionIcon = (name: string): string => {
    const icons: Record<string, string> = {
      Blinded: '👁️‍🗨️', Deafened: '👂', Frightened: '😨', Grappled: '🤜',
      Paralyzed: '🗿', Petrified: '🗿', Poisoned: '☠️', Prone: '🛌',
      Restrained: '🔗', Stunned: '💫', Unconscious: '😴', Invisible: '👻',
      Ensnared: '🌿', Charmed: '💖', Confused: '🌀', Incapacitated: '🚫',
    };
    return icons[name] || '🏷️';
  };

  const formatDuration = (duration: number): string => {
    if (duration <= 0) return '∞';
    if (duration === 1) return '1 round';
    return `${duration} rounds`;
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <ConditionIcon color="warning" />
          <Typography variant="h6">Conditions: {participant.displayName}</Typography>
          <Chip label={`${participant.conditions?.length || 0} active`} size="small" color="warning" />
        </Box>
      </DialogTitle>
      <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
        {/* Current conditions */}
        {participant.conditions && participant.conditions.length > 0 ? (
          <List dense>
            {participant.conditions.map((c: ConditionEntry, i: number) => (
              <ListItem
                key={i}
                sx={{
                  bgcolor: 'background.paper',
                  borderRadius: 1,
                  mb: 0.5,
                  border: '1px solid',
                  borderColor: 'divider',
                }}
              >
                <ListItemAvatar>
                  <Avatar sx={{ bgcolor: getConditionColor(c.name) + '.main', width: 32, height: 32, fontSize: 16 }}>
                    {getConditionIcon(c.name)}
                  </Avatar>
                </ListItemAvatar>
                <ListItemText
                  primary={
                    <Typography variant="body2" fontWeight={600}>
                      {c.name}
                    </Typography>
                  }
                  secondary={
                    <Typography variant="caption" color="text.secondary">
                      Duration: <strong>{formatDuration(c.duration)}</strong>
                      {c.description && <> · {c.description}</>}
                    </Typography>
                  }
                />
                {isEditable && (
                  <Box sx={{ display: 'flex', gap: 0.5 }}>
                    <Tooltip title="Remove condition">
                      <IconButton size="small" color="error" onClick={() => onRemoveCondition(participant.id, c.name)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Reduce duration by 1">
                      <IconButton size="small" onClick={() => {
                        if (c.duration > 0) {
                          onAddCondition(participant.id, c.name, c.duration - 1, c.description);
                          onRemoveCondition(participant.id, c.name);
                        }
                      }} disabled={c.duration <= 0}>
                        <RemoveIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Box>
                )}
              </ListItem>
            ))}
          </List>
        ) : (
          <Typography color="text.secondary" sx={{ textAlign: 'center', py: 2 }}>
            No active conditions on this participant.
          </Typography>
        )}

        {/* Add condition */}
        {isEditable && (
          <Paper sx={{ p: 2, bgcolor: 'background.default' }}>
            <Typography variant="subtitle2" gutterBottom>Add Condition:</Typography>
            <Grid container spacing={1}>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  fullWidth
                  label="Condition Name"
                  value={newConditionName}
                  onChange={e => setNewConditionName(e.target.value)}
                  placeholder="e.g., Poisoned, Grappled, Frightened"
                  autoFocus
                />
              </Grid>
              <Grid size={{ xs: 4 }}>
                <TextField
                  fullWidth
                  label="Duration (rounds, 0=∞)"
                  type="number"
                  value={newConditionDuration}
                  onChange={e => setNewConditionDuration(parseInt(e.target.value) || 0)}
                  inputProps={{ min: 0 }}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField
                  fullWidth
                  label="Description (optional)"
                  value={newConditionDesc}
                  onChange={e => setNewConditionDesc(e.target.value)}
                  size="small"
                />
              </Grid>
              <Grid size={12}>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                  Quick add:
                </Typography>
                <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                  {commonConditions.map(c => (
                    <Chip
                      key={c}
                      label={c}
                      size="small"
                      clickable
                      onClick={() => setNewConditionName(c)}
                      color={getConditionColor(c) as any}
                      variant={newConditionName === c ? 'filled' : 'outlined'}
                      sx={{ fontSize: 10 }}
                    />
                  ))}
                </Box>
              </Grid>
            </Grid>
          </Paper>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
        {isEditable && newConditionName && (
          <Button
            variant="contained"
            color="warning"
            startIcon={<ConditionIcon />}
            onClick={() => {
              onAddCondition(participant.id, newConditionName, newConditionDuration, newConditionDesc || undefined);
              setNewConditionName('');
              setNewConditionDuration(1);
              setNewConditionDesc('');
              onCloseDialog?.();
            }}
          >
            Add Condition
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

function CharacterSheetPopup({ participant, open, onClose, onHeal, onDamage }: {
  participant: CombatParticipantSummary | null; open: boolean; onClose: () => void;
  onHeal: (id: string, amount: number) => void; onDamage: (id: string, amount: number) => void;
}) {
  const [healAmount, setHealAmount] = useState(1);
  const [damageAmount, setDamageAmount] = useState(1);

  if (!participant) return null;

  const hpPercent = participant.maxHP > 0 ? (participant.currentHP / participant.maxHP) * 100 : 0;
  const hpColor = hpPercent > 60 ? '#4caf50' : hpPercent > 30 ? '#ff9800' : '#f44336';

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Avatar sx={{ width: 32, height: 32, bgcolor: participant.participantType === 'NPC' ? 'error.main' : 'primary.main' }}>
            {participant.participantType === 'NPC' ? '👹' : '👤'}
          </Avatar>
          <Typography variant="h6">{participant.displayName}</Typography>
          <Chip label={participant.participantType} size="small" color={participant.participantType === 'NPC' ? 'error' : 'primary'} />
        </Box>
      </DialogTitle>
      <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
        {/* HP */}
        <Paper sx={{ p: 2 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
            <Typography variant="body2" color="text.secondary">Hit Points</Typography>
            <Typography variant="body2" sx={{ color: hpColor, fontWeight: 'bold' }}>
              {participant.currentHP} / {participant.maxHP}
            </Typography>
          </Box>
          <Slider
            value={hpPercent}
            onChange={(_, val) => {
              const pct = val as number;
              const newHP = Math.round((pct / 100) * participant.maxHP);
              if (newHP > participant.currentHP) {
                onHeal(participant.id, newHP - participant.currentHP);
              } else {
                onDamage(participant.id, participant.currentHP - newHP);
              }
            }}
            sx={{ color: hpColor }}
            size="small"
          />
          <Box sx={{ display: 'flex', gap: 1, mt: 1 }}>
            <TextField size="small" type="number" label="Heal" value={healAmount}
              onChange={e => setHealAmount(parseInt(e.target.value) || 1)} inputProps={{ min: 1 }} sx={{ width: 80 }} />
            <Button size="small" variant="outlined" color="success" onClick={() => {
              onHeal(participant.id, healAmount); setHealAmount(1);
            }}>Heal</Button>
            <TextField size="small" type="number" label="Damage" value={damageAmount}
              onChange={e => setDamageAmount(parseInt(e.target.value) || 1)} inputProps={{ min: 1 }} sx={{ width: 80 }} />
            <Button size="small" variant="outlined" color="error" onClick={() => {
              onDamage(participant.id, damageAmount); setDamageAmount(1);
            }}>Damage</Button>
          </Box>
        </Paper>

        {/* Stats */}
        <Grid container spacing={1}>
          <Grid size={4}>
            <Paper sx={{ p: 1, textAlign: 'center' }}>
              <Typography variant="caption" color="text.secondary">AC</Typography>
              <Typography variant="h6">{participant.ac}</Typography>
            </Paper>
          </Grid>
          <Grid size={4}>
            <Paper sx={{ p: 1, textAlign: 'center' }}>
              <Typography variant="caption" color="text.secondary">Initiative</Typography>
              <Typography variant="h6">{participant.initiative}</Typography>
            </Paper>
          </Grid>
          <Grid size={4}>
            <Paper sx={{ p: 1, textAlign: 'center' }}>
              <Typography variant="caption" color="text.secondary">HP</Typography>
              <Typography variant="h6">{participant.currentHP}/{participant.maxHP}</Typography>
            </Paper>
          </Grid>
        </Grid>

        {/* Conditions */}
        {participant.conditions && participant.conditions.length > 0 && (
          <Box>
            <Typography variant="subtitle2" gutterBottom>Active Conditions</Typography>
            <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
              {participant.conditions.map((c: ConditionEntry, i: number) => (
                <Chip key={i} label={`${c.name}${c.duration > 0 ? ` (${c.duration}r)` : ''}`} size="small" color="warning" variant="outlined" />
              ))}
            </Box>
          </Box>
        )}

        {/* Death Saves */}
        <DeathSaveTracker participant={participant} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}

// ==================== Main Component ====================

export default function CombatTab({ gameId }: { gameId: string }) {
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
  const [showConditionManager, setShowConditionManager] = useState(false);
  const [showDeathSaveDialog, setShowDeathSaveDialog] = useState(false);
  const [showHealDialog, setShowHealDialog] = useState(false);
  const [showRollInitiative, setShowRollInitiative] = useState(false);
  const [showSpellDialog, setShowSpellDialog] = useState(false);
  const [showAISuggestions, setShowAISuggestions] = useState(false);
  const [showRestDialog, setShowRestDialog] = useState(false);
  const [showGridDialog, setShowGridDialog] = useState(false);
  const [showInventoryDialog, setShowInventoryDialog] = useState(false);
  const [showSANDialog, setShowSANDialog] = useState(false);
  const [showCharSheet, setShowCharSheet] = useState(false);
  const [selectedCharSheet, setSelectedCharSheet] = useState<CombatParticipantSummary | null>(null);

  // Action economy state
  const [actionEconomy, setActionEconomy] = useState<Record<string, {
    actions: number; bonusActions: number; reactions: number; movements: number;
  }>>({});

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [aiSuggestions, setAISuggestions] = useState<AISuggestions | null>(null);

  // Attack dialog state
  const [attackTarget, setAttackTarget] = useState('');
  const [attackFormula, setAttackFormula] = useState('1d20');
  const [attackBonus, setAttackBonus] = useState(0);
  const [damageFormula, setDamageFormula] = useState('1d8');
  const [damageBonus, setDamageBonus] = useState(0);
  const [weaponName, setWeaponName] = useState('Longsword');

  // Save throw dialog state
  const [saveTarget, setSaveTarget] = useState('');
  const [saveType, setSaveType] = useState('Fortitude');
  const [saveFormula, setSaveFormula] = useState('1d20');
  const [saveDC, setSaveDC] = useState(15);

  // Condition dialog state
  const [conditionTarget, setConditionTarget] = useState('');
  const [conditionName, setConditionName] = useState('');
  const [conditionDuration, setConditionDuration] = useState(1);

  // Death save dialog state
  const [deathSaveTarget, setDeathSaveTarget] = useState('');
  const [_deathSaveSuccess, setDeathSaveSuccess] = useState(true);

  // Heal dialog state
  const [healTarget, setHealTarget] = useState('');
  const [healAmount, setHealAmount] = useState(1);

  // Spell dialog state
  const [spellTarget, setSpellTarget] = useState('');
  const [spellName, setSpellName] = useState('');
  const [spellLevel, setSpellLevel] = useState('1');
  const [spellSaveFormula, setSpellSaveFormula] = useState('1d20');
  const [spellSaveDC, setSpellSaveDC] = useState(15);
  const [spellDamageFormula, setSpellDamageFormula] = useState('1d6');
  const [spellDamageBonus, setSpellDamageBonus] = useState(0);
  const [spellDescription, setSpellDescription] = useState('');

  // Add participant state
  const [newParticipantType, setNewParticipantType] = useState('NPC');
  const [newDisplayName, setNewDisplayName] = useState('');
  const [newAC, setNewAC] = useState(10);
  const [newHP, setNewHP] = useState(10);
  const [newMaxHP, setNewMaxHP] = useState(10);

  // Grid state
  const [gridWidth, setGridWidth] = useState(20);
  const [gridHeight, setGridHeight] = useState(15);

  // SAN state
  const [sanTarget, setSanTarget] = useState('');
  const [sanLoss, setSanLoss] = useState(1);
  const [sanRecovery, setSanRecovery] = useState(1);
  const [sanDC, setSanDC] = useState(20);

  // Inventory state
  const [invItemName, setInvItemName] = useState('');
  const [invItemType, setInvItemType] = useState('Weapon');
  const [invQuantity, setInvQuantity] = useState(1);

  const loadActiveCombats = useCallback(async () => {
    try {
      const combats = await invoke('GetActiveCombats', gameId);
      if (combats) setActiveCombats(combats);
    } catch { /* ignore */ }
  }, [gameId, invoke]);

  const loadActiveCombat = useCallback(async () => {
    if (activeCombats.length === 0) { setActiveCombat(null); return; }
    try {
      const log = await invoke('GetCombatLog', activeCombats[0].id);
      if (log) setActiveCombat(log);
    } catch { /* ignore */ }
  }, [activeCombats, invoke]);

  useEffect(() => { loadActiveCombats(); }, [gameId]);

  useEffect(() => {
    if (!isConnected) return;
    on('CombatStarted', () => loadActiveCombat());
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
    on('ActionSpent', (data: any) => {
      setActionEconomy(prev => ({
        ...prev,
        [data.participantId]: { ...prev[data.participantId], actions: data.actionsRemaining ?? 0 }
      }));
      loadActiveCombat();
    });
    on('BonusActionSpent', (data: any) => {
      setActionEconomy(prev => ({
        ...prev,
        [data.participantId]: { ...prev[data.participantId], bonusActions: data.bonusActionsRemaining ?? 0 }
      }));
      loadActiveCombat();
    });
    on('ReactionSpent', (data: any) => {
      setActionEconomy(prev => ({
        ...prev,
        [data.participantId]: { ...prev[data.participantId], reactions: data.reactionsRemaining ?? 0 }
      }));
      loadActiveCombat();
    });
    on('MovementSpent', (data: any) => {
      setActionEconomy(prev => ({
        ...prev,
        [data.participantId]: { ...prev[data.participantId], movements: data.movementsRemaining ?? 0 }
      }));
      loadActiveCombat();
    });
    on('ActionsRefreshed', (data: any) => {
      setActionEconomy(prev => ({
        ...prev,
        [data.participantId]: {
          actions: data.actionsRemaining ?? 1,
          bonusActions: data.bonusActionsRemaining ?? 0,
          reactions: data.reactionsRemaining ?? 1,
          movements: data.movementsRemaining ?? 1,
        }
      }));
      loadActiveCombat();
    });
    on('ActionsSet', (data: any) => {
      setActionEconomy(prev => ({
        ...prev,
        [data.participantId]: {
          actions: data.actionsRemaining ?? 1,
          bonusActions: data.bonusActionsRemaining ?? 0,
          reactions: data.reactionsRemaining ?? 1,
          movements: data.movementsRemaining ?? 1,
        }
      }));
      loadActiveCombat();
    });
    on('CombatGridSet', () => loadActiveCombat());
    on('CombatPositionSet', () => loadActiveCombat());
    on('CombatMove', () => loadActiveCombat());
    on('CombatAutoResolved', () => loadActiveCombat());
    return () => {};
  }, [isConnected, on, loadActiveCombat]);

  // ==================== Action Economy Handlers ====================

  const handleSpendAction = async (participantId: string) => {
    if (!activeCombat) return;
    try {
      await invoke('CombatSpendAction', activeCombat.combatId, participantId);
      setSuccess('Action spent');
      setTimeout(() => setSuccess(null), 1500);
    } catch (e: any) { setError(e.message); }
  };

  const handleSpendBonusAction = async (participantId: string) => {
    if (!activeCombat) return;
    try {
      await invoke('CombatSpendBonusAction', activeCombat.combatId, participantId);
      setSuccess('Bonus action spent');
      setTimeout(() => setSuccess(null), 1500);
    } catch (e: any) { setError(e.message); }
  };

  const handleSpendReaction = async (participantId: string) => {
    if (!activeCombat) return;
    try {
      await invoke('CombatSpendReaction', activeCombat.combatId, participantId);
      setSuccess('Reaction spent');
      setTimeout(() => setSuccess(null), 1500);
    } catch (e: any) { setError(e.message); }
  };

  const handleSpendMovement = async (participantId: string) => {
    if (!activeCombat) return;
    try {
      await invoke('CombatSpendMovement', activeCombat.combatId, participantId);
      setSuccess('Movement spent');
      setTimeout(() => setSuccess(null), 1500);
    } catch (e: any) { setError(e.message); }
  };

  const handleRefreshActions = async (participantId: string) => {
    if (!activeCombat) return;
    try {
      await invoke('CombatRefreshActions', activeCombat.combatId, participantId);
      setSuccess('Actions refreshed');
      setTimeout(() => setSuccess(null), 1500);
    } catch (e: any) { setError(e.message); }
  };

  // ==================== Handlers ====================

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
  const handleRetreatTurn = async () => { if (!activeCombat) return; try { await invoke('RetreatTurn', activeCombat.combatId); } catch (e: any) { setError(e.message); } };

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
      await invoke('CombatSaveThrow', activeCombat.combatId, saveTarget, saveTarget, saveType, saveFormula, saveDC);
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

  const handleViewCharSheet = (participant: CombatParticipantSummary) => {
    setSelectedCharSheet(participant);
    setShowCharSheet(true);
  };

  // ==================== Derived Data ====================

  const getCurrentTurnParticipant = () => {
    if (!activeCombat || activeCombat.participants.length === 0) return null;
    const idx = Math.min(activeCombat.currentTurnIndex, activeCombat.participants.length - 1);
    return activeCombat.participants[idx];
  };

  const sortedParticipants = activeCombat ? [...activeCombat.participants].sort((a: CombatParticipantSummary, b: CombatParticipantSummary) => b.initiative - a.initiative) : [];
  const currentTurn = getCurrentTurnParticipant();

  // ==================== Render ====================

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

  return (
    <>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CombatIcon color="error" fontSize="large" />
          <Typography variant="h5">{activeCombat.name || 'Combat'}</Typography>
          <Chip label={activeCombat.status} size="small" color={activeCombat.status === 'Active' ? 'success' : 'default'} />
          <Chip label={`Round ${activeCombat.currentRound}`} size="small" variant="outlined" />
          <Chip label={`${activeCombat.participants.length} participants`} size="small" variant="outlined" />
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
          {currentTurn && <Chip icon={<SheetIcon />} label="View Sheet" size="small" clickable onClick={() => handleViewCharSheet(currentTurn)} sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />}
        </Box>
        <Box sx={{ flex: 1 }} />
        <Box sx={{ display: 'flex', gap: 0.5 }}>
          <Button size="small" variant="outlined" startIcon={<TurnIcon />} onClick={handleRetreatTurn} sx={{ color: 'white', borderColor: 'rgba(255,255,255,0.5)' }}>◀</Button>
          <Button size="small" variant="contained" startIcon={<TurnIcon />} onClick={handleAdvanceTurn}>Next ▶</Button>
        </Box>
      </Paper>

      {/* Action Bar */}
      <Paper sx={{ p: 1, mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
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
        <Button size="small" variant="outlined" startIcon={<ConditionIcon />} onClick={() => setShowConditionManager(true)}>Conditions</Button>
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
                    <TableCell>AC</TableCell><TableCell>Init</TableCell><TableCell>Actions</TableCell><TableCell>Conditions</TableCell>
                    <TableCell sx={{ textAlign: 'right' }}>Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {sortedParticipants.map((p: CombatParticipantSummary, idx: number) => (
                    <TableRow key={p.id} sx={{
                      bgcolor: p.isCurrentTurn ? 'primary.light' : 'inherit',
                      opacity: p.isDead ? 0.5 : 1,
                      borderLeft: p.isCurrentTurn ? `4px solid ${'primary.main'}` : 'transparent',
                    }}>
                      <TableCell><Chip label={idx + 1} size="small" color={p.isCurrentTurn ? 'primary' : 'default'} variant={p.isCurrentTurn ? 'filled' : 'outlined'} /></TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                          <Avatar sx={{ width: 20, height: 20, fontSize: 10, bgcolor: p.participantType === 'NPC' ? 'error.main' : 'primary.main' }}>
                            {p.participantType === 'NPC' ? '👹' : '👤'}
                          </Avatar>
                          <Typography variant="body2" sx={{ fontWeight: p.isCurrentTurn ? 'bold' : 'normal' }}>{p.displayName}</Typography>
                          {p.isCurrentTurn && <Chip label="TURN" size="small" color="primary" variant="filled" sx={{ height: 16, fontSize: 9 }} />}
                        </Box>
                      </TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                          <HPIcon fontSize="small" sx={{ color: p.currentHP < p.maxHP * 0.3 ? 'error.main' : 'success.main' }} />
                          <Typography variant="body2" sx={{ color: p.currentHP < p.maxHP * 0.3 ? 'error.main' : 'inherit', fontWeight: p.isCurrentTurn ? 'bold' : 'normal' }}>
                            {p.currentHP}/{p.maxHP}
                          </Typography>
                        </Box>
                        <DeathSaveTracker participant={p} />
                      </TableCell>
                      <TableCell><Typography variant="body2">{p.ac}</Typography></TableCell>
                      <TableCell><Typography variant="body2">{p.initiative}</Typography></TableCell>
                      <TableCell>
                        <ActionEconomyTracker
                          participant={p}
                          actionsRemaining={actionEconomy[p.id]?.actions}
                          bonusActionsRemaining={actionEconomy[p.id]?.bonusActions}
                          reactionsRemaining={actionEconomy[p.id]?.reactions}
                          movementsRemaining={actionEconomy[p.id]?.movements}
                          onSpendAction={() => handleSpendAction(p.id)}
                          onSpendBonusAction={() => handleSpendBonusAction(p.id)}
                          onSpendReaction={() => handleSpendReaction(p.id)}
                          onSpendMovement={() => handleSpendMovement(p.id)}
                          onRefresh={() => handleRefreshActions(p.id)}
                          isEditable={user?.role === 'Creator'}
                        />
                      </TableCell>
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
                          <Tooltip title="View Sheet"><IconButton size="small" onClick={() => handleViewCharSheet(p)}><SheetIcon fontSize="small" /></IconButton></Tooltip>
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
            <CombatLogPanel events={activeCombat.events} showCombatLog={showCombatLog} />
          </Collapse>
          {!showCombatLog && (
            <Paper sx={{ p: 2, textAlign: 'center' }}>
              <IconButton onClick={() => setShowCombatLog(true)}>
                <ExpandMoreIcon />
              </IconButton>
              <Typography variant="body2" color="text.secondary">
                Click to expand combat log ({activeCombat.events.length} events)
              </Typography>
            </Paper>
          )}
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
          <Button onClick={() => { setDeathSaveTarget(deathSaveTarget); setDeathSaveSuccess(false); handleDeathSave(false); }} variant="outlined" color="error" startIcon={<CancelIcon />}>Failed</Button>
          <Button onClick={() => { setDeathSaveTarget(deathSaveTarget); setDeathSaveSuccess(true); handleDeathSave(true); }} variant="contained" color="success" startIcon={<CheckCircleIcon />}>Success</Button>
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
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {[1, 5, 10, 25, 50].map(v => (
              <Chip key={v} label={`+${v}`} size="small" clickable onClick={() => setHealAmount(v)} />
            ))}
          </Box>
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

      <Dialog open={showInventoryDialog} onClose={() => setShowInventoryDialog(false)} maxWidth="md" fullWidth>
        <DialogTitle>Inventory</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <FormControl fullWidth sx={{ mb: 2 }}>
            <InputLabel>Target</InputLabel>
            <Select value={sanTarget} label="Target" onChange={e => setSanTarget(e.target.value)}>
              {sortedParticipants.map(p => (<MenuItem key={p.id} value={p.id}>{p.displayName}</MenuItem>))}
            </Select>
          </FormControl>
          <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
            <TextField size="small" label="Item Name" value={invItemName} onChange={e => setInvItemName(e.target.value)} sx={{ flex: 1 }} />
            <Select size="small" value={invItemType} onChange={e => setInvItemType(e.target.value)} sx={{ width: 120 }}>
              <MenuItem value="Weapon">Weapon</MenuItem>
              <MenuItem value="Armor">Armor</MenuItem>
              <MenuItem value="Shield">Shield</MenuItem>
              <MenuItem value="Item">Item</MenuItem>
              <MenuItem value="Consumable">Consumable</MenuItem>
              <MenuItem value="Magic">Magic Item</MenuItem>
            </Select>
            <TextField size="small" type="number" label="Qty" value={invQuantity} onChange={e => setInvQuantity(parseInt(e.target.value) || 1)} inputProps={{ min: 1, max: 99 }} sx={{ width: 60 }} />
            <Button variant="contained" size="small" onClick={handleAddItem} disabled={!invItemName.trim()}>Add</Button>
          </Box>
          <Divider sx={{ my: 1 }} />
          <Typography variant="subtitle2" gutterBottom>Items on {sortedParticipants.find(p => p.id === sanTarget)?.displayName || 'selected'}:</Typography>
          <List dense sx={{ maxHeight: 300, overflow: 'auto' }}>
            {/* Note: inventory is stored in participant notes/JSON, so we show a placeholder */}
            <ListItem>
              <ListItemText primary={<Typography color="text.secondary" variant="body2">Items are stored per-participant in the combat state.</Typography>} />
            </ListItem>
          </List>
        </DialogContent>
        <DialogActions><Button onClick={() => setShowInventoryDialog(false)}>Close</Button></DialogActions>
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

      {/* Condition Manager */}
      <ConditionManager
        participant={selectedCharSheet}
        open={showConditionManager}
        onClose={() => setShowConditionManager(false)}
        onCloseDialog={() => setShowConditionManager(false)}
        onRemoveCondition={(pid, name) => handleRemoveCondition(pid, name)}
        onAddCondition={(pid, name, duration, desc) => {
          if (desc) {
            invoke('CombatApplyCondition', activeCombat?.combatId, pid, name, duration, desc);
          } else {
            invoke('CombatApplyCondition', activeCombat?.combatId, pid, name, duration);
          }
          loadActiveCombat();
        }}
        isEditable={user?.role === 'Creator'}
      />

      {/* Character Sheet Popup */}
      {selectedCharSheet && (
        <CharacterSheetPopup
          participant={selectedCharSheet}
          open={showCharSheet}
          onClose={() => { setShowCharSheet(false); setSelectedCharSheet(null); }}
          onHeal={(id, amount) => { setHealTarget(id); setHealAmount(amount); setShowHealDialog(true); }}
          onDamage={(id, amount) => { setHealTarget(id); setHealAmount(amount); }}
        />
      )}
    </>
  );
}
