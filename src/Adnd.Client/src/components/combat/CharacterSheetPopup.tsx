import { useState } from 'react';
import { Box, Typography, Paper, IconButton, Collapse, TextField, Button } from '@mui/material';
import { Add as AddIcon, Remove as RemoveIcon, Close as CloseIcon } from '@mui/icons-material';
import type { CombatParticipantSummary } from '../../types/combat.types';

export default function CharacterSheetPopup({ participant, open, onClose, onHeal, onDamage }: {
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

