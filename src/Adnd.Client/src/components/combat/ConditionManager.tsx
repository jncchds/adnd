import { Box, Typography, Chip, Button, Paper, IconButton, Collapse, Dialog, DialogTitle, DialogContent, DialogActions, Divider, TextField, MenuItem, Select, FormControl, InputLabel } from '@mui/material';
import { Add as AddIcon, Remove as RemoveIcon, Close as CloseIcon, ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon } from '@mui/icons-material';
import type { CombatParticipantSummary, ConditionEntry } from '../../types/combat.types';

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

