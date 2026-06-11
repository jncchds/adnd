import { Box, Typography, Chip, Button, Divider, IconButton, Collapse } from '@mui/material';
import { ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon } from '@mui/icons-material';
import type { CombatParticipantSummary } from '../../types/combat.types';

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

