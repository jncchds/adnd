import { useState } from 'react';
import { usePlotWeaver } from '../api/gameHooks';
import {
  Box, Typography, Paper, Chip, LinearProgress, Collapse, IconButton,
  TextField, Button, List, ListItem, ListItemText, ListItemSecondaryAction,
  Dialog, DialogTitle, DialogContent, DialogActions, Alert, AlertTitle,
  Tooltip,
} from '@mui/material';
import {
  ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon,
  Refresh as RefreshIcon, ArrowUpward as UpIcon, ArrowDownward as DownIcon,
  History as HistoryIcon, Lightbulb as BulbIcon,
} from '@mui/icons-material';
import { PlotThreadCategory, PlotReviewResponse, ThreadUpdate, StoryOpportunityResponse } from '../types/plot.types';

const CATEGORY_LABELS: Record<PlotThreadCategory, string> = {
  [PlotThreadCategory.General]: 'General',
  [PlotThreadCategory.Faction]: 'Faction',
  [PlotThreadCategory.Mystery]: 'Mystery',
  [PlotThreadCategory.Personal]: 'Personal',
  [PlotThreadCategory.Threat]: 'Threat',
  [PlotThreadCategory.WorldEvent]: 'World Event',
  [PlotThreadCategory.Relationship]: 'Relationship',
};

const CATEGORY_COLORS: Record<PlotThreadCategory, 'default' | 'primary' | 'secondary' | 'error' | 'warning' | 'success' | 'info'> = {
  [PlotThreadCategory.General]: 'default',
  [PlotThreadCategory.Faction]: 'primary',
  [PlotThreadCategory.Mystery]: 'info',
  [PlotThreadCategory.Personal]: 'success',
  [PlotThreadCategory.Threat]: 'error',
  [PlotThreadCategory.WorldEvent]: 'warning',
  [PlotThreadCategory.Relationship]: 'secondary',
};

const MOMENTUM_COLORS: Record<string, string> = {
  urgent: '#f44336',
  high: '#ff9800',
  moderate: '#ffeb3b',
  low: '#4caf50',
  abandoned: '#9e9e9e',
};

function getMomentumLabel(momentum: number): string {
  if (momentum >= 7) return 'Urgent';
  if (momentum >= 4) return 'High';
  if (momentum >= 1) return 'Moderate';
  if (momentum >= -2) return 'Low';
  return 'Fading';
}

function getMomentumColor(momentum: number): string {
  if (momentum >= 7) return MOMENTUM_COLORS.urgent;
  if (momentum >= 4) return MOMENTUM_COLORS.high;
  if (momentum >= 1) return MOMENTUM_COLORS.moderate;
  if (momentum >= -2) return MOMENTUM_COLORS.low;
  return MOMENTUM_COLORS.abandoned;
}

interface Props {
  gameId: string;
}

export default function PlotBoardTab({ gameId }: Props) {
  const { threads, reviews, isLoading, error, refetch, fetchReviews, triggerReview, adjustMomentum, detectOpportunities } = usePlotWeaver(gameId || undefined);
  const [expandedThread, setExpandedThread] = useState<string | null>(null);
  const [expandedReview, setExpandedReview] = useState<string | null>(null);
  const [reviewDialog, setReviewDialog] = useState(false);
  const [reviewContext, setReviewContext] = useState('');
  const [reviewResult, setReviewResult] = useState<PlotReviewResponse | null>(null);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const [opportunities, setOpportunities] = useState<StoryOpportunityResponse[]>([]);
  const [adjustingThread, setAdjustingThread] = useState<string | null>(null);
  const [adjustDelta, setAdjustDelta] = useState(1);
  const [adjustReason, setAdjustReason] = useState('');

  const handleDetectOpportunities = async () => {
    setOpportunities([]);
    try {
      const data = await detectOpportunities();
      setOpportunities(data);
    } catch (e: any) {
      console.error('Failed to detect opportunities:', e);
    }
  };

  const handleReview = async () => {
    setReviewError(null);
    setReviewResult(null);
    try {
      const result = await triggerReview(reviewContext || undefined);
      setReviewResult(result);
      setReviewContext('');
    } catch (e: any) {
      setReviewError(e.message);
    }
  };

  const handleAdjust = async (threadId: string) => {
    await adjustMomentum(threadId, adjustDelta, adjustReason || 'Manual adjustment');
    setAdjustingThread(null);
    setAdjustDelta(1);
    setAdjustReason('');
  };

  const activeThreads = threads.filter(t => t.status === 'Active');
  const resolvedThreads = threads.filter(t => t.status === 'Resolved');
  const abandonedThreads = threads.filter(t => t.status === 'Abandoned');

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, height: '100%', overflow: 'auto' }}>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Plot Board</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Tooltip title="Detect story opportunities">
            <IconButton size="small" onClick={handleDetectOpportunities} disabled={isLoading}>
              <BulbIcon />
            </IconButton>
          </Tooltip>
          <Tooltip title="Refresh plot state">
            <IconButton size="small" onClick={() => setReviewDialog(true)} disabled={isLoading}>
              <RefreshIcon />
            </IconButton>
          </Tooltip>
          <Tooltip title="View review history">
            <IconButton size="small" onClick={fetchReviews}>
              <HistoryIcon />
            </IconButton>
          </Tooltip>
          <Tooltip title="Refresh threads">
            <IconButton size="small" onClick={refetch} disabled={isLoading}>
              <RefreshIcon />
            </IconButton>
          </Tooltip>
        </Box>
      </Box>

      {/* Error */}
      {error && (
        <Alert severity="error" onClose={() => {}}>
          <AlertTitle>Error</AlertTitle>
          {error}
        </Alert>
      )}

      {isLoading && <LinearProgress />}

      {/* Active Threads */}
      {activeThreads.length > 0 && (
        <Paper elevation={1} sx={{ p: 2 }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
            Active Threads ({activeThreads.length})
          </Typography>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            {activeThreads.sort((a, b) => b.relevanceScore - a.relevanceScore).map(thread => (
              <Paper
                key={thread.id}
                elevation={0}
                sx={{
                  p: 1.5,
                  border: '1px solid',
                  borderColor: getMomentumColor(thread.momentum),
                  borderRadius: 1,
                  cursor: 'pointer',
                  '&:hover': { bgcolor: 'action.hover' },
                }}
                onClick={() => setExpandedThread(expandedThread === thread.id ? null : thread.id)}
              >
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Typography variant="body2" fontWeight="bold">{thread.title}</Typography>
                    <Chip
                      label={CATEGORY_LABELS[thread.category]}
                      size="small"
                      color={CATEGORY_COLORS[thread.category]}
                      variant="outlined"
                    />
                  </Box>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Typography variant="caption" color={getMomentumColor(thread.momentum)}>
                      {getMomentumLabel(thread.momentum)}
                    </Typography>
                    {expandedThread === thread.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                  </Box>
                </Box>

                {/* Momentum bar */}
                <Box sx={{ mt: 0.5, display: 'flex', alignItems: 'center', gap: 1 }}>
                  <Box sx={{ flex: 1 }}>
                    <LinearProgress
                      variant="determinate"
                      value={(thread.momentum + 10) / 20 * 100}
                      sx={{
                        height: 6,
                        borderRadius: 3,
                        bgcolor: 'background.paper',
                        '& .MuiLinearProgress-bar': {
                          bgcolor: getMomentumColor(thread.momentum),
                        },
                      }}
                    />
                  </Box>
                  <Typography variant="caption" color="text.secondary">
                    {thread.momentum >= 0 ? '+' : ''}{thread.momentum.toFixed(1)}
                  </Typography>
                </Box>

                <Collapse in={expandedThread === thread.id}>
                  <Box sx={{ mt: 1.5, pt: 1.5, borderTop: '1px solid', borderColor: 'divider' }}>
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                      {thread.description}
                    </Typography>
                    {thread.nextMilestone && (
                      <Typography variant="body2" sx={{ mb: 0.5 }}>
                        <strong>Next:</strong> {thread.nextMilestone}
                      </Typography>
                    )}
                    {thread.foreshadowing && (
                      <Typography variant="body2" sx={{ mb: 0.5 }}>
                        <strong>Hint:</strong> {thread.foreshadowing}
                      </Typography>
                    )}
                    {thread.milestoneEvents && thread.milestoneEvents.length > 0 && (
                      <Box sx={{ mb: 1 }}>
                        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                          Milestones ({thread.milestoneEvents.length}):
                        </Typography>
                        {thread.milestoneEvents.map(m => (
                          <Chip
                            key={m.id}
                            label={m.title}
                            size="small"
                            color={m.status === 'Completed' ? 'success' : m.status === 'Triggered' ? 'warning' : 'default'}
                            variant={m.status === 'Completed' ? 'filled' : 'outlined'}
                            sx={{ mr: 0.5, mb: 0.5 }}
                          />
                        ))}
                      </Box>
                    )}
                    {thread.adaptationHistory.length > 0 && (
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                        Last adapted: {thread.adaptationHistory[thread.adaptationHistory.length - 1]}
                      </Typography>
                    )}
                    {/* Quick adjust */}
                    <Box sx={{ mt: 1, display: 'flex', alignItems: 'center', gap: 1 }}>
                      <IconButton size="small" onClick={(e) => { e.stopPropagation(); setAdjustingThread(thread.id); setAdjustDelta(-1); }}>
                        <DownIcon fontSize="small" />
                      </IconButton>
                      <Typography variant="caption" color="text.secondary">
                        Adjust momentum
                      </Typography>
                      <IconButton size="small" onClick={(e) => { e.stopPropagation(); setAdjustingThread(thread.id); setAdjustDelta(1); }}>
                        <UpIcon fontSize="small" />
                      </IconButton>
                    </Box>
                  </Box>
                </Collapse>
              </Paper>
            ))}
          </Box>
        </Paper>
      )}

      {/* Resolved Threads */}
      {resolvedThreads.length > 0 && (
        <Paper elevation={0} sx={{ p: 1.5, opacity: 0.6 }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 0.5 }}>
            Resolved ({resolvedThreads.length})
          </Typography>
          {resolvedThreads.map(thread => (
            <Typography key={thread.id} variant="body2" sx={{ textDecoration: 'line-through', color: 'text.secondary' }}>
              {thread.title}
            </Typography>
          ))}
        </Paper>
      )}

      {/* Abandoned Threads */}
      {abandonedThreads.length > 0 && (
        <Paper elevation={0} sx={{ p: 1.5, opacity: 0.4 }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 0.5 }}>
            Abandoned ({abandonedThreads.length})
          </Typography>
          {abandonedThreads.map(thread => (
            <Typography key={thread.id} variant="body2" sx={{ color: 'text.disabled' }}>
              {thread.title}
            </Typography>
          ))}
        </Paper>
      )}

      {/* Review History */}
      {reviews.length > 0 && (
        <Paper elevation={0} sx={{ p: 1.5 }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
            Review History ({reviews.length})
          </Typography>
          <List dense>
            {reviews.map(review => (
              <ListItem
                key={review.id}
                sx={{
                  borderBottom: '1px solid',
                  borderColor: 'divider',
                  cursor: 'pointer',
                  '&:hover': { bgcolor: 'action.hover' },
                }}
                onClick={() => setExpandedReview(expandedReview === review.id ? null : review.id)}
              >
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Chip label={review.trigger} size="small" variant="outlined" />
                      <Typography variant="body2">{review.summary}</Typography>
                    </Box>
                  }
                  secondary={`Reviewed at: ${new Date(review.reviewedAt).toLocaleString()}`}
                />
                {expandedReview === review.id ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                <ListItemSecondaryAction>
                  <Typography variant="caption" color="text.secondary">
                    {new Date(review.reviewedAt).toLocaleDateString()}
                  </Typography>
                </ListItemSecondaryAction>
              </ListItem>
            ))}
          </List>
        </Paper>
      )}

      {/* Story Opportunities */}
      {opportunities.length > 0 && (
        <Paper elevation={1} sx={{ p: 2, bgcolor: 'info.lighter' }}>
          <Typography variant="subtitle2" color="info.dark" sx={{ mb: 1 }}>
            💡 Story Opportunities ({opportunities.length})
          </Typography>
          {opportunities.map((opp, i) => (
            <Box key={i} sx={{ mb: 1, pb: 1, borderBottom: '1px solid', borderColor: 'divider' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                <Chip
                  label={opp.type}
                  size="small"
                  color={opp.type === 'NewThread' ? 'success' : opp.type === 'SpawnMilestone' ? 'warning' : 'default'}
                />
                <Typography variant="body2" fontWeight="bold">{opp.title}</Typography>
              </Box>
              <Typography variant="body2" color="text.secondary">{opp.description}</Typography>
              {opp.newThreadTitle && (
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                  New thread: {opp.newThreadTitle} ({opp.newThreadCategory})
                </Typography>
              )}
            </Box>
          ))}
          <Typography variant="caption" color="info.dark" sx={{ fontStyle: 'italic' }}>
            These opportunities were auto-applied by the AI-GM.
          </Typography>
        </Paper>
      )}

      {/* Manual Review Dialog */}
      <Dialog open={reviewDialog} onClose={() => setReviewDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Refresh Plot State</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography variant="body2" color="text.secondary">
            The LLM will review all active plot threads in light of recent game events and adapt them accordingly.
            Optionally add context to guide the review.
          </Typography>
          <TextField
            fullWidth
            multiline
            rows={3}
            label="Additional Context (optional)"
            value={reviewContext}
            onChange={e => setReviewContext(e.target.value)}
            placeholder="e.g., Players just discovered a major betrayal..."
          />
          {reviewError && (
            <Alert severity="error">
              <AlertTitle>Error</AlertTitle>
              {reviewError}
            </Alert>
          )}
          {reviewResult && (
            <Alert severity="success">
              <AlertTitle>Review Complete</AlertTitle>
              <Typography variant="body2">{reviewResult.summary}</Typography>
              {reviewResult.updates.length > 0 && (
                <Box sx={{ mt: 1 }}>
                  {reviewResult.updates.map((u: ThreadUpdate, i: number) => (
                    <Typography key={i} variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                      • {u.threadTitle}: {u.oldMomentum?.toFixed(1)} → {u.newMomentum?.toFixed(1)} ({u.reason})
                    </Typography>
                  ))}
                </Box>
              )}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReviewDialog(false)}>Cancel</Button>
          <Button onClick={handleReview} variant="contained" disabled={isLoading}>
            {isLoading ? 'Reviewing...' : 'Review'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Momentum Adjustment Dialog */}
      <Dialog open={adjustingThread !== null} onClose={() => setAdjustingThread(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Adjust Momentum</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            type="number"
            fullWidth
            label="Delta (-10 to +10)"
            value={adjustDelta}
            onChange={e => setAdjustDelta(parseInt(e.target.value) || 0)}
            inputProps={{ min: -10, max: 10 }}
          />
          <TextField
            fullWidth
            label="Reason"
            value={adjustReason}
            onChange={e => setAdjustReason(e.target.value)}
            placeholder="Why are you adjusting this?"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAdjustingThread(null)}>Cancel</Button>
          <Button onClick={() => adjustingThread && handleAdjust(adjustingThread)} variant="contained">
            Apply
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
