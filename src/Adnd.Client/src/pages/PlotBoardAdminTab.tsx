import { useState, useCallback } from 'react';
import { usePlotWeaver } from '../api/gameHooks';
import {
  Box, Typography, Paper, Button, Chip, List, ListItem, ListItemText,
  ListItemSecondaryAction, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Alert, AlertTitle, LinearProgress,
} from '@mui/material';

interface Props {
  threads: any[];
  isLoading: boolean;
  gameId: string;
}

function PlotBoardAdminTab({ threads, isLoading, gameId }: Props) {
  const [expandedThread, setExpandedThread] = useState<string | null>(null);
  const [expandedReview, setExpandedReview] = useState<string | null>(null);
  const [reviews, setReviews] = useState<any[]>([]);
  const [isLoadingReviews, setIsLoadingReviews] = useState(false);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const [reviewDialog, setReviewDialog] = useState(false);
  const [reviewContext, setReviewContext] = useState('');
  const [reviewResult, setReviewResult] = useState<any>(null);
  const [opportunities, setOpportunities] = useState<any[]>([]);
  const [detecting, setDetecting] = useState(false);

  const { triggerReview, detectOpportunities, refetch } = usePlotWeaver(gameId);

  const fetchReviews = useCallback(async () => {
    setIsLoadingReviews(true);
    try {
      const res = await fetch(`/api/admin/games/${gameId}/plot-weaver/reviews?limit=20`, {
        headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
      });
      if (res.ok) setReviews(await res.json());
    } catch (e: any) {
      setReviewError(e.message);
    } finally {
      setIsLoadingReviews(false);
    }
  }, []);

  const handleReview = async () => {
    setReviewError(null);
    setReviewResult(null);
    try {
      const result = await triggerReview(reviewContext || undefined);
      setReviewResult(result);
      setReviewContext('');
      await refetch();
    } catch (e: any) {
      setReviewError(e.message);
    }
  };

  const handleDetectOpportunities = async () => {
    setDetecting(true);
    setOpportunities([]);
    try {
      const data = await detectOpportunities();
      setOpportunities(data);
      await refetch();
    } catch (e: any) {
      console.error('Failed to detect opportunities:', e);
    } finally {
      setDetecting(false);
    }
  };

  const activeThreads = threads.filter((t: any) => t.status === 'Active');
  const resolvedThreads = threads.filter((t: any) => t.status === 'Resolved');
  const abandonedThreads = threads.filter((t: any) => t.status === 'Abandoned');

  const getMomentumColor = (momentum: number) => {
    if (momentum >= 7) return '#f44336';
    if (momentum >= 4) return '#ff9800';
    if (momentum >= 1) return '#ffeb3b';
    if (momentum >= -2) return '#4caf50';
    return '#9e9e9e';
  };

  const getMomentumLabel = (momentum: number) => {
    if (momentum >= 7) return 'Urgent';
    if (momentum >= 4) return 'High';
    if (momentum >= 1) return 'Moderate';
    if (momentum >= -2) return 'Low';
    return 'Fading';
  };

  const categoryLabels: Record<number, string> = {
    0: 'General', 1: 'Faction', 2: 'Mystery', 3: 'Personal',
    4: 'Threat', 5: 'WorldEvent', 6: 'Relationship',
  };

  const categoryColors: Record<number, 'default' | 'primary' | 'secondary' | 'error' | 'warning' | 'success' | 'info'> = {
    0: 'default', 1: 'primary', 2: 'info', 3: 'success',
    4: 'error', 5: 'warning', 6: 'secondary',
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Plot Board</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button size="small" variant="outlined" onClick={handleDetectOpportunities} disabled={detecting}>
            {detecting ? 'Detecting...' : '💡 Detect Opportunities'}
          </Button>
          <Button size="small" variant="outlined" onClick={() => setReviewDialog(true)} disabled={isLoading}>
            🔄 Review Plot State
          </Button>
          <Button size="small" variant="outlined" onClick={fetchReviews} disabled={isLoadingReviews}>
            📜 Review History
          </Button>
          <Button size="small" variant="outlined" onClick={refetch} disabled={isLoading}>
            🔄 Refresh
          </Button>
        </Box>
      </Box>

      {reviewError && <Alert severity="error">{reviewError}</Alert>}

      {opportunities.length > 0 && (
        <Paper elevation={1} sx={{ p: 2, bgcolor: 'info.lighter' }}>
          <Typography variant="subtitle2" color="info.dark" sx={{ mb: 1 }}>
            💡 Story Opportunities ({opportunities.length})
          </Typography>
          {opportunities.map((opp: any, i: number) => (
            <Box key={i} sx={{ mb: 1, pb: 1, borderBottom: '1px solid', borderColor: 'divider' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                <Chip label={opp.type} size="small" color={opp.type === 'NewThread' ? 'success' : opp.type === 'SpawnMilestone' ? 'warning' : 'default'} />
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

      {activeThreads.length > 0 && (
        <Paper elevation={1} sx={{ p: 2 }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
            Active Threads ({activeThreads.length})
          </Typography>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            {activeThreads
              .sort((a: any, b: any) => b.relevanceScore - a.relevanceScore)
              .map((thread: any) => (
                <Paper
                  key={thread.id}
                  elevation={0}
                  sx={{
                    p: 1.5, border: '1px solid', borderColor: getMomentumColor(thread.momentum),
                    borderRadius: 1, cursor: 'pointer', '&:hover': { bgcolor: 'action.hover' },
                  }}
                  onClick={() => setExpandedThread(expandedThread === thread.id ? null : thread.id)}
                >
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Typography variant="body2" fontWeight="bold">{thread.title}</Typography>
                      <Chip label={categoryLabels[thread.category] || 'General'} size="small" color={categoryColors[thread.category] || 'default'} variant="outlined" />
                    </Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Typography variant="caption" color={getMomentumColor(thread.momentum)}>
                        {getMomentumLabel(thread.momentum)}
                      </Typography>
                      {expandedThread === thread.id ? '▾' : '▸'}
                    </Box>
                  </Box>
                  <Box sx={{ mt: 0.5, display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Box sx={{ flex: 1 }}>
                      <LinearProgress variant="determinate" value={(thread.momentum + 10) / 20 * 100}
                        sx={{ height: 6, borderRadius: 3, bgcolor: 'background.paper',
                          '& .MuiLinearProgress-bar': { bgcolor: getMomentumColor(thread.momentum) } }} />
                    </Box>
                    <Typography variant="caption" color="text.secondary">
                      {thread.momentum >= 0 ? '+' : ''}{thread.momentum.toFixed(1)}
                    </Typography>
                  </Box>
                  {expandedThread === thread.id && (
                    <Box sx={{ mt: 1.5, pt: 1.5, borderTop: '1px solid', borderColor: 'divider' }}>
                      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{thread.description}</Typography>
                      {thread.nextMilestone && <Typography variant="body2" sx={{ mb: 0.5 }}><strong>Next:</strong> {thread.nextMilestone}</Typography>}
                      {thread.foreshadowing && <Typography variant="body2" sx={{ mb: 0.5 }}><strong>Hint:</strong> {thread.foreshadowing}</Typography>}
                      {thread.milestoneEvents && thread.milestoneEvents.length > 0 && (
                        <Box sx={{ mb: 1 }}>
                          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                            Milestones ({thread.milestoneEvents.length}):
                          </Typography>
                          {thread.milestoneEvents.map((m: any) => (
                            <Chip key={m.id} label={m.title} size="small"
                              color={m.status === 'Completed' ? 'success' : m.status === 'Triggered' ? 'warning' : 'default'}
                              variant={m.status === 'Completed' ? 'filled' : 'outlined'} sx={{ mr: 0.5, mb: 0.5 }} />
                          ))}
                        </Box>
                      )}
                      {thread.adaptationHistory.length > 0 && (
                        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                          Last adapted: {thread.adaptationHistory[thread.adaptationHistory.length - 1]}
                        </Typography>
                      )}
                    </Box>
                  )}
                </Paper>
              ))}
          </Box>
        </Paper>
      )}

      {resolvedThreads.length > 0 && (
        <Paper elevation={0} sx={{ p: 1.5, opacity: 0.6 }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 0.5 }}>Resolved ({resolvedThreads.length})</Typography>
          {resolvedThreads.map((t: any) => (
            <Typography key={t.id} variant="body2" sx={{ textDecoration: 'line-through', color: 'text.secondary' }}>{t.title}</Typography>
          ))}
        </Paper>
      )}

      {abandonedThreads.length > 0 && (
        <Paper elevation={0} sx={{ p: 1.5, opacity: 0.4 }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 0.5 }}>Abandoned ({abandonedThreads.length})</Typography>
          {abandonedThreads.map((t: any) => (
            <Typography key={t.id} variant="body2" sx={{ color: 'text.disabled' }}>{t.title}</Typography>
          ))}
        </Paper>
      )}

      {reviews.length > 0 && (
        <Paper elevation={0} sx={{ p: 1.5 }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>Review History ({reviews.length})</Typography>
          <List dense>
            {reviews.map((review: any) => (
              <ListItem key={review.id} sx={{ borderBottom: '1px solid', borderColor: 'divider', cursor: 'pointer', '&:hover': { bgcolor: 'action.hover' } }}
                onClick={() => setExpandedReview(expandedReview === review.id ? null : review.id)}>
                <ListItemText
                  primary={<Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Chip label={review.trigger} size="small" variant="outlined" />
                    <Typography variant="body2">{review.summary}</Typography>
                  </Box>}
                  secondary={`Reviewed at: ${new Date(review.reviewedAt).toLocaleString()}`} />
                {expandedReview === review.id ? '▾' : '▸'}
                <ListItemSecondaryAction>
                  <Typography variant="caption" color="text.secondary">{new Date(review.reviewedAt).toLocaleDateString()}</Typography>
                </ListItemSecondaryAction>
              </ListItem>
            ))}
          </List>
        </Paper>
      )}

      <Dialog open={reviewDialog} onClose={() => setReviewDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Review Plot State</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography variant="body2" color="text.secondary">
            The LLM will review all active plot threads in light of recent game events and adapt them accordingly.
          </Typography>
          <TextField fullWidth multiline rows={3} label="Additional Context (optional)" value={reviewContext}
            onChange={e => setReviewContext(e.target.value)} placeholder="e.g., Players just discovered a major betrayal..." />
          {reviewError && <Alert severity="error">{reviewError}</Alert>}
          {reviewResult && (
            <Alert severity="success">
              <AlertTitle>Review Complete</AlertTitle>
              <Typography variant="body2">{reviewResult.summary}</Typography>
              {reviewResult.updates?.length > 0 && (
                <Box sx={{ mt: 1 }}>
                  {reviewResult.updates.map((u: any, i: number) => (
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
          <Button onClick={handleReview} variant="contained">Review</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

export default PlotBoardAdminTab;
