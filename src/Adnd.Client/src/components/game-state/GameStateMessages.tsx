import { Box, Typography, Paper, Chip, Divider, Button } from '@mui/material';
import MarkdownRenderer from '../MarkdownRenderer';

interface GameStateMessagesProps {
  gameState: any;
}

export default function GameStateMessages({ gameState }: GameStateMessagesProps) {
  if (!gameState?.RecentMessages) return <Typography>No message data available.</Typography>;

  const getMessageIcon = (type: number) => {
    switch (type) {
      case 0: return '💬'; case 1: return '🤫'; case 2: return '📢'; case 3: return '🤫';
      case 5: return '🎲'; case 4: return '⚔️'; case 7: return '🧙';
      case 8: return '🤖'; case 9: return '📡'; case 86: return '📖'; case 93: return '🔧';
      case 20: return '⚔️'; case 21: return '🏁'; case 60: return '👤'; case 61: return '👋';
      case 62: return '🔌'; case 63: return '✅';
      default: return '📝';
    }
  };

  const getMessageColor = (type: number): 'primary' | 'info' | 'default' | 'warning' | 'error' | 'success' | 'secondary' => {
    switch (type) {
      case 0: return 'primary'; case 1: return 'info'; case 2: return 'default'; case 3: return 'default';
      case 5: return 'warning'; case 4: return 'error'; case 7: return 'success';
      case 8: return 'info'; case 9: return 'info'; case 86: return 'primary'; case 93: return 'secondary';
      default: return 'default';
    }
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
      <Typography variant="h6">Recent Messages ({gameState.RecentMessages.length})</Typography>
      {gameState.RecentMessages.length === 0 ? (
        <Typography color="text.secondary">No messages yet.</Typography>
      ) : (
        <Box sx={{ maxHeight: 500, overflow: 'auto', bgcolor: 'background.default', p: 1, borderRadius: 1 }}>
          {[...gameState.RecentMessages].reverse().map((m: any) => (
            <Box key={m.id} sx={{
              p: 1, mb: 0.5, borderRadius: 1,
              borderLeft: `3px solid`,
              borderColor: getMessageColor(m.type),
              bgcolor: m.isOOC ? 'action.hover' : 'background.paper',
            }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                <Typography variant="caption">{getMessageIcon(m.type)}</Typography>
                <Chip label={m.type} size="small" color={getMessageColor(m.type) as any} variant="outlined" />
                <Typography variant="caption" fontWeight="bold">{m.playerName}</Typography>
                {m.isOOC && <Chip label="OOC" size="small" color="default" variant="filled" />}
                <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
                  {new Date(m.createdAt).toLocaleString()}
                </Typography>
              </Box>
              <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: m.type === MessageType.Dice ? 'monospace' : 'inherit' }}>
                {m.content}
              </Typography>
            </Box>
          ))}
        </Box>
      )}
    </Box>
  );
}

// ==================== LLM Tab ====================
