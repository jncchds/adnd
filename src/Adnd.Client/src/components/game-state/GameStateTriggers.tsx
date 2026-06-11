import { Box, Typography, Paper, Chip, Button, Alert, CircularProgress } from '@mui/material';
import { Warning as WarningIcon } from '@mui/icons-material';

interface GameStateTriggersProps {
  gameState: any;
  onTrigger: (trigger: string) => void;
  loading: boolean;
  onOpenReview: () => void;
  onOpenAgent: () => void;
}

export default function GameStateTriggers({ gameState, onTrigger, loading, onOpenReview, onOpenAgent }: GameStateTriggersProps) {
  if (!gameState?.Game) return null;
  const isRunning = gameState!.Game.gmStatus === 'Running';

  const triggerGroups = [
    {
      title: '⚙️ System Simulation',
      description: 'Trigger core game lifecycle events manually',
      triggers: [
        { label: 'Pause Game', endpoint: 'pause', desc: 'Publish GamePaused event' },
        { label: 'Resume Game', endpoint: 'resume', desc: 'Publish GameResumed event' },
        { label: 'Start Combat', endpoint: 'combat-start', desc: 'Create dummy combat and publish CombatStarted' },
        { label: 'End Combat', endpoint: 'combat-end', desc: 'End latest active combat and publish CombatEnded' },
      ],
    },
    {
      title: '📖 Narrative',
      description: 'Trigger the GM agent to generate narrative content',
      triggers: [
        { label: 'Narrate', endpoint: 'narrate', desc: 'Generate a narrative continuation' },
        { label: 'New Scene', endpoint: 'new-scene', desc: 'Create a new scene' },
        { label: 'GM Evaluate', endpoint: 'gm-evaluate', desc: 'Evaluate current game state' },
      ],
    },
    {
      title: '🤖 Suggestions',
      description: 'Get AI suggestions and plot ideas',
      triggers: [
        { label: 'Suggest', endpoint: 'suggest', desc: 'Get plot continuation suggestions' },
        { label: 'Detect Opportunities', endpoint: 'detect-opportunities', desc: 'Find story opportunities' },
        { label: 'Plot Check', endpoint: 'plot-check', desc: 'Check for plot opportunities' },
      ],
    },
    {
      title: '📊 Plot Management',
      description: 'Manage plot threads and milestones',
      triggers: [
        { label: 'Generate Threads', endpoint: 'generate-threads', desc: 'Generate new plot threads' },
        { label: 'Spawn Milestones', endpoint: 'spawn-milestones', desc: 'Spawn milestones for high-momentum threads' },
        { label: 'Full Review', endpoint: '', desc: 'Review all plot threads', action: onOpenReview },
      ],
    },
    {
      title: '🔍 Consistency & RAG',
      description: 'Check consistency and generate summaries',
      triggers: [
        { label: 'Consistency Check', endpoint: 'consistency', desc: 'Check plot consistency' },
        { label: 'Session Summary', endpoint: 'session-summary', desc: 'Generate session summary' },
      ],
    },
    {
      title: '🤖 Agent Framework',
      description: 'Manually create agent calls',
      triggers: [
        { label: 'New Agent Call', endpoint: '', desc: 'Create a custom agent call', action: onOpenAgent },
      ],
    },
  ];

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Manual Event Triggers</Typography>
        <Chip label={isRunning ? '✅ GM Agent Running' : '⚠️ GM Agent Not Running'}
          size="small"
          color={isRunning ? 'success' : 'warning'}
          variant={isRunning ? 'filled' : 'outlined'} />
      </Box>

      {!isRunning && (
        <Alert severity="warning" icon={<WarningIcon />}>
          GM agent is not running. Most triggers require the GM agent to be in 'Running' state.
          Start the game or resume the GM agent first.
        </Alert>
      )}

      {triggerGroups.map((group, gi) => (
        <Paper key={gi} sx={{ p: 2 }}>
          <Typography variant="subtitle1" sx={{ mb: 0.5 }}>{group.title}</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>{group.description}</Typography>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {group.triggers.map((t: any, ti: number) => (
              <Button
                key={ti}
                size="small"
                variant="outlined"
                disabled={!isRunning && !t.action}
                onClick={() => t.action ? t.action() : onTrigger(t.endpoint)}
                startIcon={loading === t.label ? <CircularProgress size={16} /> : undefined}
                sx={{ textTransform: 'none' }}
              >
                {t.label}
                {t.desc && <Typography variant="caption" sx={{ ml: 0.5, opacity: 0.7 }}>{t.desc}</Typography>}
              </Button>
            ))}
          </Box>
        </Paper>
      ))}
    </Box>
  );
}

