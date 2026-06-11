import React from 'react';
import { Box, Typography, Paper, IconButton, Collapse, Divider, Chip } from '@mui/material';
import { ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon } from '@mui/icons-material';
import MarkdownRenderer from '../MarkdownRenderer';
import type { PlotThreadResponse } from '../../types/plot.types';

interface SectionCardProps {
  title: string;
  icon?: React.ReactNode;
  expanded: boolean;
  onToggle: () => void;
  children: React.ReactNode;
}

function SectionCard({ title, icon, expanded, onToggle, children }: SectionCardProps) {
  return (
    <Paper>
      <Box sx={{
        p: 1.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        cursor: 'pointer', '&:hover': { bgcolor: 'action.hover' },
      }} onClick={onToggle}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          {icon}
          <Typography variant="subtitle1" fontWeight="bold">{title}</Typography>
        </Box>
        {expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
      </Box>
      <Collapse in={expanded}>
        <Divider />
        <Box sx={{ p: 2 }}>{children}</Box>
      </Collapse>
    </Paper>
  );
}

interface StatCardProps {
  label: string;
  value: React.ReactNode;
  sub?: string;
  color?: 'success' | 'error' | 'warning' | 'info' | 'default';
}

function StatCard({ label, value, sub }: StatCardProps) {
  return (
    <Paper sx={{ p: 1.5, bgcolor: 'background.default' }}>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>{label}</Typography>
      <Typography variant="h6" fontWeight="bold">{value}</Typography>
      {sub && <Typography variant="caption" color="text.secondary">{sub}</Typography>}
    </Paper>
  );
}

interface PlotThreadCardProps {
  thread: any;
}

function PlotThreadCard({ thread }: PlotThreadCardProps) {
  const getMomentumColor = (momentum: number) => {
    if (momentum >= 7) return '#f44336';
    if (momentum >= 4) return '#ff9800';
    if (momentum >= 1) return '#ffeb3b';
    if (momentum >= -2) return '#4caf50';
    return '#9e9e9e';
  };

  const getMomentumLabel = (momentum: number) => {
    if (momentum >= 7) return '🔥 Urgent';
    if (momentum >= 4) return '⚡ High';
    if (momentum >= 1) return '📈 Moderate';
    if (momentum >= -2) return '📉 Low';
    return '💤 Fading';
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
    <Paper sx={{ p: 2, borderLeft: `4px solid ${getMomentumColor(thread.momentum)}` }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="body1" fontWeight="bold">{thread.title}</Typography>
          <Chip label={categoryLabels[thread.category] || 'General'} size="small" color={categoryColors[thread.category] || 'default'} variant="outlined" />
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Chip label={thread.status} size="small"
            color={thread.status === 'Active' ? 'success' : thread.status === 'Resolved' ? 'default' : 'error'}
            variant={thread.status === 'Active' ? 'filled' : 'outlined'} />
        </Box>
      </Box>

      {/* Momentum Bar */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <Typography variant="caption" color={getMomentumColor(thread.momentum)} sx={{ minWidth: 70 }}>
          {getMomentumLabel(thread.momentum)}
        </Typography>
        <Box sx={{ flex: 1 }}>
          <LinearProgress variant="determinate" value={(thread.momentum + 10) / 20 * 100}
            sx={{ height: 8, borderRadius: 4, bgcolor: 'background.paper',
              '& .MuiLinearProgress-bar': { bgcolor: getMomentumColor(thread.momentum), borderRadius: 4 } }} />
        </Box>
        <Typography variant="caption" fontWeight="bold" color={getMomentumColor(thread.momentum)}>
          {thread.momentum >= 0 ? '+' : ''}{thread.momentum.toFixed(1)}
        </Typography>
        <Chip label={`Relevance: ${(thread.relevanceScore * 100).toFixed(0)}%`} size="small" />
      </Box>

      {/* Description */}
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{thread.description}</Typography>

      {/* Details */}
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
        {thread.nextMilestone && (
          <Chip label={`Next: ${thread.nextMilestone}`} size="small" color="info" variant="outlined" />
        )}
        {thread.foreshadowing && (
          <Chip label={`Foreshadowing: ${thread.foreshadowing}`} size="small" color="warning" variant="outlined" />
        )}
        {thread.milestoneEvents && thread.milestoneEvents.length > 0 && (
          <Chip label={`${thread.milestoneEvents.length} milestones`} size="small" />
        )}
      </Box>

      {/* Adaptation History */}
      {thread.adaptationHistory && thread.adaptationHistory.length > 0 && (
        <Box sx={{ mt: 1 }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
            Last adaptation:
          </Typography>
          <Typography variant="caption" sx={{ display: 'block', color: 'text.secondary', fontStyle: 'italic' }}>
            {thread.adaptationHistory[thread.adaptationHistory.length - 1]}
          </Typography>
        </Box>
      )}

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
        Updated {thread.updatedAt ? new Date(thread.updatedAt).toLocaleString() : '—'}
      </Typography>
    </Paper>
  );
}

