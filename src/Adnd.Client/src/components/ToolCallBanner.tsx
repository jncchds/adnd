import { Box, Typography, Button, Paper, Chip, Collapse, IconButton, Divider, List, ListItem, ListItemText, ListItemAvatar, Avatar } from '@mui/material';
import { Close as CloseIcon, Check as CheckIcon, Replay as ReplayIcon, Warning as WarningIcon } from '@mui/icons-material';
import type { ToolCallInfo } from '../api/client';
import { useState } from 'react';

export interface PendingToolCall extends ToolCallInfo {
  argumentsParsed?: Record<string, unknown>;
}

interface ToolCallBannerProps {
  pendingCalls: PendingToolCall[];
  onConfirm: (toolCallId: string, approved: boolean) => Promise<void | null>;
  onRoll: (toolCallId: string) => Promise<void>;
  onDecline: (toolCallId: string) => Promise<void>;
  onDismiss: (toolCallId: string) => void;
  isCreator: boolean;
}

const TOOL_ICONS: Record<string, string> = {
  'askPlayerToRoll': '🎲',
  'askPlayerToRollDice': '🎲',
  'narrate': '📖',
  'queryRAG': '📚',
  'queryPlotThreads': '📋',
  'queryCharacter': '👤',
  'queryPlayers': '👥',
  'manageState': '⚙️',
  'rollDice': '🎲',
  'rollSkillCheck': '📋',
  'rollAttack': '⚔️',
  'startCombat': '⚔️',
  'addCombatant': '👤',
  'applyDamage': '💥',
  'applyCondition': '🏷️',
  'endCombat': '🏁',
  'createNPC': '🧙',
  'updateNPC': '✏️',
};

const TOOL_LABELS: Record<string, string> = {
  'askPlayerToRoll': 'Skill Check Roll',
  'askPlayerToRollDice': 'Dice Roll',
  'narrate': 'Narrate',
  'queryRAG': 'Search Plot',
  'queryPlotThreads': 'Plot Threads',
  'queryCharacter': 'Character Info',
  'queryPlayers': 'Player List',
  'manageState': 'Update State',
  'rollDice': 'Roll Dice',
  'rollSkillCheck': 'NPC Skill Check',
  'rollAttack': 'NPC Attack',
  'startCombat': 'Start Combat',
  'addCombatant': 'Add Combatant',
  'applyDamage': 'Deal Damage',
  'applyCondition': 'Apply Condition',
  'endCombat': 'End Combat',
  'createNPC': 'Create NPC',
  'updateNPC': 'Update NPC',
};

export default function ToolCallBanner({ pendingCalls, onConfirm, onRoll, onDecline, onDismiss, isCreator }: ToolCallBannerProps) {
  const [expanded, setExpanded] = useState(true);

  const playerRollCalls = pendingCalls.filter(c =>
    c.toolName === 'askPlayerToRoll' || c.toolName === 'askPlayerToRollDice'
  );

  const gmCalls = pendingCalls.filter(c =>
    c.toolName !== 'askPlayerToRoll' && c.toolName !== 'askPlayerToRollDice'
  );

  if (pendingCalls.length === 0) return null;

  const hasPlayerRolls = playerRollCalls.length > 0;
  const hasGMActions = gmCalls.length > 0;

  return (
    <Paper
      sx={{
        position: 'relative',
        mb: 2,
        bgcolor: hasPlayerRolls ? 'warning.lighter' : 'info.lighter',
        border: hasPlayerRolls ? '1px solid' : '1px solid',
        borderColor: hasPlayerRolls ? 'warning.main' : 'info.main',
        overflow: 'hidden',
      }}
    >
      {/* Header */}
      <Box sx={{
        p: 1.5,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        cursor: 'pointer',
        bgcolor: hasPlayerRolls ? 'warning.lighter' : 'info.lighter',
      }}
        onClick={() => setExpanded(!expanded)}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          {hasPlayerRolls && (
            <>
              <WarningIcon color="warning" fontSize="small" />
              <Typography variant="subtitle2" color="warning.dark" fontWeight={600}>
                {playerRollCalls.length} player roll{playerRollCalls.length > 1 ? 's' : ''} pending
              </Typography>
            </>
          )}
          {hasGMActions && (
            <>
              <ReplayIcon color="info" fontSize="small" />
              <Typography variant="subtitle2" color="info.dark" fontWeight={600}>
                {gmCalls.length} GM action{gmCalls.length > 1 ? 's' : ''} pending
              </Typography>
            </>
          )}
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <Chip
            label={`${pendingCalls.length} pending`}
            size="small"
            color={hasPlayerRolls ? 'warning' : 'info'}
            sx={{ height: 20, fontSize: 10 }}
          />
          <IconButton size="small" onClick={(e) => { e.stopPropagation(); setExpanded(!expanded); }}>
            <CloseIcon fontSize="small" />
          </IconButton>
        </Box>
      </Box>

      {/* Expanded content */}
      <Collapse in={expanded}>
        <Divider />

        {/* Player roll requests */}
        {hasPlayerRolls && (
          <Box sx={{ p: 1.5, bgcolor: 'warning.lighter' }}>
            <Typography variant="caption" color="warning.dark" fontWeight={600} sx={{ display: 'block', mb: 1 }}>
              🎲 Player Roll Requests
            </Typography>
            <List dense>
              {playerRollCalls.map(call => (
                <ToolRollItem
                  key={call.id}
                  call={call}
                  onRoll={onRoll}
                  onDecline={onDecline}
                />
              ))}
            </List>
          </Box>
        )}

        {/* GM actions (Creator only) */}
        {isCreator && hasGMActions && (
          <Box sx={{ p: 1.5, bgcolor: 'info.lighter' }}>
            <Typography variant="caption" color="info.dark" fontWeight={600} sx={{ display: 'block', mb: 1 }}>
              ⚙️ GM Actions (need confirmation)
            </Typography>
            <List dense>
              {gmCalls.map(call => (
                <ToolActionItem
                  key={call.id}
                  call={call}
                  onConfirm={onConfirm}
                  onDismiss={onDismiss}
                />
              ))}
            </List>
          </Box>
        )}
      </Collapse>
    </Paper>
  );
}

// ==================== Player Roll Request Item ====================

interface ToolRollItemProps {
  call: PendingToolCall;
  onRoll: (toolCallId: string) => Promise<void>;
  onDecline: (toolCallId: string) => Promise<void>;
}

function ToolRollItem({ call, onRoll, onDecline }: ToolRollItemProps) {
  const args = call.argumentsParsed as any || {};
  const skill = args.skill || '';
  const formula = args.formula || '1d20';
  const dc = args.dc || 15;
  const context = args.context || '';
  const optional = args.optional === true;
  void (args.playerIds || args.playerId || 'all'); // consumed for side-effect

  return (
    <ListItem sx={{
      bgcolor: 'background.paper',
      borderRadius: 1,
      mb: 0.5,
      border: '1px solid',
      borderColor: 'warning.lighter',
    }}>
      <ListItemAvatar>
        <Avatar sx={{ bgcolor: 'warning.main', width: 28, height: 28, fontSize: 14 }}>
          🎲
        </Avatar>
      </ListItemAvatar>
      <ListItemText
        primary={
          <Typography variant="body2" fontWeight={600}>
            {skill ? `Roll ${skill}` : `Roll ${formula}`}
            {skill ? ` (DC ${dc})` : ''}
          </Typography>
        }
        secondary={
          <Typography variant="caption" color="text.secondary">
            {context || 'GM requested a roll'}
            {optional && ' (optional)'}
          </Typography>
        }
      />
      <Box sx={{ display: 'flex', gap: 0.5 }}>
        <Button
          size="small"
          variant="contained"
          color="success"
          startIcon={<CheckIcon />}
          onClick={() => onRoll(call.id)}
          sx={{ fontSize: 11, height: 28 }}
        >
          Roll
        </Button>
        {optional && (
          <Button
            size="small"
            variant="outlined"
            color="warning"
            onClick={() => onDecline(call.id)}
            sx={{ fontSize: 11, height: 28 }}
          >
            Decline
          </Button>
        )}
      </Box>
    </ListItem>
  );
}

// ==================== GM Action Item ====================

interface ToolActionItemProps {
  call: PendingToolCall;
  onConfirm: (toolCallId: string, approved: boolean) => Promise<void | null>;
  onDismiss: (toolCallId: string) => void;
}

function ToolActionItem({ call, onConfirm, onDismiss: _onDismiss }: ToolActionItemProps) {
  const label = TOOL_LABELS[call.toolName] || call.toolName;

  return (
    <ListItem sx={{
      bgcolor: 'background.paper',
      borderRadius: 1,
      mb: 0.5,
      border: '1px solid',
      borderColor: 'info.lighter',
    }}>
      <ListItemAvatar>
        <Avatar sx={{ bgcolor: 'info.main', width: 28, height: 28, fontSize: 14 }}>
          {TOOL_ICONS[call.toolName] || '⚙️'}
        </Avatar>
      </ListItemAvatar>
      <ListItemText
        primary={<Typography variant="body2" fontWeight={600}>{label}</Typography>}
        secondary={
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
            {call.outputMessage || 'Awaiting confirmation'}
          </Typography>
        }
      />
      <Box sx={{ display: 'flex', gap: 0.5 }}>
        <Button
          size="small"
          variant="contained"
          color="success"
          startIcon={<CheckIcon />}
          onClick={() => onConfirm(call.id, true)}
          sx={{ fontSize: 11, height: 28 }}
        >
          Approve
        </Button>
        <Button
          size="small"
          variant="outlined"
          color="error"
          onClick={() => onConfirm(call.id, false)}
          sx={{ fontSize: 11, height: 28 }}
        >
          Deny
        </Button>
      </Box>
    </ListItem>
  );
}
