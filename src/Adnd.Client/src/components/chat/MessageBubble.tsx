import { Box, Typography, Chip } from '@mui/material';
import MarkdownRenderer from '../MarkdownRenderer';
import { MESSAGE_STYLES } from './messageStyles';
import type { UnifiedMessage } from '../../api/hooks/useMessages';
import { MARKDOWN_TYPES, hasMarkdownSyntax } from './markdownUtils';

export default function MessageBubble({ msg }: { msg: UnifiedMessage }) {
  const style = MESSAGE_STYLES[msg.type];
  const isWhisper = msg.isWhisper || msg.type === 'inGameWhisper' || msg.type === 'oocWhisper';
  const isSystem = msg.isSystem || ['dice', 'skillCheck', 'attack', 'spellCast', 'combatStart', 'combatEnd', 'combatPause', 'combatResume',
    'initiative', 'initiativeComplete', 'turnAdvanced', 'turnRetreated', 'turnSet', 'damage', 'heal', 'deathSave',
    'conditionApplied', 'conditionRemoved', 'xpGranted', 'levelUp', 'sanLoss', 'sanRecovery', 'sanCheck',
    'actionSpent', 'bonusActionSpent', 'reactionSpent', 'movementSpent', 'actionsRefreshed', 'participantAdded',
    'participantRemoved', 'gridSet', 'positionSet', 'combatMove', 'itemAdded', 'itemRemoved', 'itemEquipped',
    'itemUnequipped', 'playerJoined', 'playerLeft', 'playerRoleChanged',
    'characterCreated', 'characterUpdated', 'sessionCreated', 'sessionClosed', 'gameStarted', 'gamePaused',
    'gameResumed', 'gameArchived', 'system', 'agentCall', 'agentResponse', 'toolCall', 'toolCallConfirmed',
    'toolCallDenied', 'playerRollRequest', 'playerRollConfirmed', 'playerRollDeclined', 'playerRollResult',
    'stateChange', 'aiCombatSuggestion', 'aiCombatAutoResolve'].includes(msg.type);

  // Determine if this is a system/notification message (rendered more subtly)
  const isNotification = ['combatStart', 'combatEnd', 'combatPause', 'combatResume', 'initiative', 'initiativeComplete',
    'turnAdvanced', 'turnRetreated', 'turnSet', 'participantAdded', 'participantRemoved', 'playerJoined', 'playerLeft',
    'playerRoleChanged', 'characterCreated', 'characterUpdated',
    'sessionCreated', 'sessionClosed', 'gameStarted', 'gamePaused', 'gameResumed', 'gameArchived', 'stateChange',
    'gridSet', 'positionSet', 'combatMove', 'itemAdded', 'itemRemoved', 'itemEquipped', 'itemUnequipped',
    'actionSpent', 'bonusActionSpent', 'reactionSpent', 'movementSpent', 'actionsRefreshed', 'xpGranted', 'levelUp',
    'sanLoss', 'sanRecovery', 'sanCheck', 'deathSave', 'conditionApplied', 'conditionRemoved',
    'toolCall', 'toolCallConfirmed', 'toolCallDenied', 'playerRollRequest', 'playerRollConfirmed', 'playerRollDeclined',
    'aiCombatSuggestion', 'aiCombatAutoResolve'].includes(msg.type);

  return (
    <Box sx={{
      mb: isNotification ? 0.5 : 1,
      p: isNotification ? 0.75 : 1.5,
      borderRadius: 2,
      bgcolor: style.bg,
      borderLeft: `3px solid ${style.border}`,
      opacity: isNotification ? 0.85 : 1,
    }}>
      {/* Header: Type chip + sender + time */}
      <Box sx={{ display: 'flex', gap: 0.75, alignItems: 'center', flexWrap: 'wrap', mb: isNotification ? 0.25 : 0.5 }}>
        <Chip
          label={`${style.chipIcon} ${style.chipLabel}`}
          size="small"
          color={style.chipColor}
          sx={{ height: 18, fontSize: 10, fontWeight: 600 }}
        />
        {isWhisper && (
          <Chip
            label={`🤫 → ${msg.whisperTo || 'Unknown'}`}
            size="small"
            color="warning"
            sx={{ height: 18, fontSize: 9 }}
          />
        )}
        <Typography variant="caption" sx={{ fontWeight: 600, color: 'text.primary' }}>
          {msg.senderName}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {new Date(msg.timestamp).toLocaleTimeString()}
        </Typography>
      </Box>

      {/* Content — markdown for text messages, plain for system messages */}
      {MARKDOWN_TYPES.has(msg.type) && hasMarkdownSyntax(msg.content) ? (
        <Box sx={{
          color: isWhisper ? 'text.secondary' : 'text.primary',
          fontStyle: isWhisper ? 'italic' : 'normal',
          wordBreak: 'break-word',
        }}>
          <MarkdownRenderer content={msg.content} compact />
        </Box>
      ) : (
        <Typography variant="body2" sx={{
          color: isSystem ? 'text.secondary' : 'text.primary',
          fontStyle: isWhisper ? 'italic' : 'normal',
          wordBreak: 'break-word',
        }}>
          {msg.content}
        </Typography>
      )}

      {/* Extra info for dice */}
      {msg.type === 'dice' && msg.diceRolls && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Rolls: [{msg.diceRolls.join(', ')}]
        </Typography>
      )}

      {/* Extra info for skill checks */}
      {msg.type === 'skillCheck' && msg.skillResult && (
        <Chip
          label={msg.skillResult === 'success' ? '✓ Success' : '✗ Failure'}
          size="small"
          color={msg.skillResult === 'success' ? 'success' : 'error'}
          sx={{ mt: 0.5, height: 20, fontSize: 10 }}
        />
      )}

      {/* Extra info for attacks */}
      {msg.type === 'attack' && (
        <Box sx={{ mt: 0.5 }}>
          <Chip
            label={msg.attackHit ? '✓ Hit' : '✗ Miss'}
            size="small"
            color={msg.attackHit ? 'success' : 'error'}
            sx={{ height: 20, fontSize: 10, mr: 0.5 }}
          />
          {msg.attackDamage && (
            <Chip
              label={`${msg.attackDamage} damage`}
              size="small"
              color="warning"
              sx={{ height: 20, fontSize: 10 }}
            />
          )}
        </Box>
      )}

      {/* Extra info for combat participants */}
      {msg.type === 'participantAdded' && msg.participantType && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Type: {msg.participantType}{msg.hp != null ? ` · HP: ${msg.hp}/${msg.maxHP}` : ''}{msg.ac != null ? ` · AC: ${msg.ac}` : ''}
        </Typography>
      )}

      {/* Extra info for initiative */}
      {msg.type === 'initiative' && msg.diceRolls && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Rolls: [{msg.diceRolls.join(', ')}]
        </Typography>
      )}

      {/* Extra info for conditions */}
      {msg.type === 'conditionApplied' && msg.conditionDuration != null && (
        <Chip
          label={`Duration: ${msg.conditionDuration}`}
          size="small"
          color="warning"
          sx={{ mt: 0.5, height: 20, fontSize: 10 }}
        />
      )}

      {/* Extra info for damage/heal */}
      {(msg.type === 'damage' || msg.type === 'heal') && msg.hp != null && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          HP: {msg.hp}/{msg.maxHP}
        </Typography>
      )}

      {/* Extra info for action economy */}
      {(msg.type === 'actionSpent' || msg.type === 'bonusActionSpent' || msg.type === 'reactionSpent' || msg.type === 'movementSpent') && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          {msg.actionsRemaining != null ? `Actions: ${msg.actionsRemaining} ` : ''}
          {msg.bonusActionsRemaining != null ? `Bonus: ${msg.bonusActionsRemaining} ` : ''}
          {msg.reactionsRemaining != null ? `Reactions: ${msg.reactionsRemaining} ` : ''}
          {msg.movementsRemaining != null ? `Movement: ${msg.movementsRemaining}` : ''}
        </Typography>
      )}

      {/* Extra info for agent calls */}
      {msg.type === 'agentCall' && msg.agentStatus && (
        <Chip
          label={msg.agentStatus}
          size="small"
          color={msg.agentStatus === 'Completed' ? 'success' : msg.agentStatus === 'Running' ? 'warning' : 'error'}
          sx={{ mt: 0.5, height: 20, fontSize: 10 }}
        />
      )}

      {/* Extra info for player roll */}
      {msg.type === 'playerRollRequest' && msg.skill && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Skill: {msg.skill} · DC: {msg.skillDC}
        </Typography>
      )}
    </Box>
  );
}

