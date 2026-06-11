import { Box, Card, CardContent, Typography, Chip, LinearProgress, Collapse, IconButton, Paper, Grid, Divider } from '@mui/material';
import { ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon } from '@mui/icons-material';
import { useState } from 'react';
import type { GameProviderUsageSummary } from '../../types/llm.types';

export function formatTokens(tokens: number): string {
  if (!tokens) return '0';
  if (tokens >= 1000000) return `${(tokens / 1000000).toFixed(1)}M`;
  if (tokens >= 1000) return `${(tokens / 1000).toFixed(1)}k`;
  return tokens.toString();
}

export function formatDuration(ms: number): string {
  if (!ms) return '0s';
  if (ms >= 3600000) return `${(ms / 3600000).toFixed(1)}h`;
  if (ms >= 60000) return `${(ms / 60000).toFixed(1)}m`;
  return `${(ms / 1000).toFixed(1)}s`;
}

export function timeAgo(dateStr?: string): string {
  if (!dateStr) return '—';
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  return `${days}d ago`;
}

interface SummaryCardProps {
  label: string;
  value: string;
  sublabel?: string;
  color?: 'primary' | 'success' | 'warning' | 'error';
  icon?: string;
}

export function SummaryCard({ label, value, sublabel, color = 'primary', icon }: SummaryCardProps) {
  const colorMap: Record<string, string> = { primary: 'success', success: 'success', warning: 'warning', error: 'error' };
  return (
    <Card sx={{ height: '100%', bgcolor: 'background.paper', borderRadius: 2 }}>
      <CardContent sx={{ p: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
          {icon && <Typography variant="h4" sx={{ fontSize: '1.5rem' }}>{icon}</Typography>}
          <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>{label}</Typography>
        </Box>
        <Typography variant="h4" fontWeight={700} color={colorMap[color]}>{value}</Typography>
        {sublabel && <Typography variant="caption" color="text.secondary">{sublabel}</Typography>}
      </CardContent>
    </Card>
  );
}

interface ProviderRowProps {
  summary: GameProviderUsageSummary;
}

export default function ProviderRow({ summary }: ProviderRowProps) {
  const [expanded, setExpanded] = useState(false);
  const successRate = summary.totalCalls > 0 ? Math.round((summary.successfulCalls / summary.totalCalls) * 100) : 0;

  return (
    <Paper sx={{ mb: 1 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', p: 1.5, cursor: 'pointer', gap: 1 }} onClick={() => setExpanded(!expanded)}>
        <Typography variant="body2" fontWeight={600}>{summary.providerType}</Typography>
        <Typography variant="caption" color="text.secondary">· {summary.model}</Typography>
        <Box sx={{ flex: 1 }} />
        <Chip label={`${summary.totalCalls} calls`} size="small" variant="outlined" />
        <Chip label={`${formatTokens(summary.totalTokens)} tokens`} size="small" variant="outlined" />
        <LinearProgress variant="determinate" value={successRate} sx={{ width: 60, height: 6, borderRadius: 3, ml: 1 }} color={successRate >= 90 ? 'success' : successRate >= 70 ? 'warning' : 'error'} />
        <Typography variant="caption" color="text.secondary" sx={{ ml: 0.5 }}>{successRate}%</Typography>
        <IconButton size="small" onClick={e => e.stopPropagation()}>{expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}</IconButton>
      </Box>
      <Collapse in={expanded}>
        <Divider />
        <Box sx={{ p: 2 }}>
          <Grid container spacing={2}>
            <Grid size={4}><Typography variant="caption" color="text.secondary">Total Calls</Typography><Typography variant="h6">{summary.totalCalls}</Typography></Grid>
            <Grid size={4}><Typography variant="caption" color="text.secondary">Successful</Typography><Typography variant="h6" color="success.main">{summary.successfulCalls}</Typography></Grid>
            <Grid size={4}><Typography variant="caption" color="text.secondary">Failed</Typography><Typography variant="h6" color="error.main">{summary.failedCalls}</Typography></Grid>
            <Grid size={4}><Typography variant="caption" color="text.secondary">Total Tokens</Typography><Typography variant="h6">{formatTokens(summary.totalTokens)}</Typography></Grid>
            <Grid size={4}><Typography variant="caption" color="text.secondary">Prompt Tokens</Typography><Typography variant="h6">{formatTokens(summary.totalPromptTokens)}</Typography></Grid>
            <Grid size={4}><Typography variant="caption" color="text.secondary">Completion Tokens</Typography><Typography variant="h6">{formatTokens(summary.totalCompletionTokens)}</Typography></Grid>
            <Grid size={4}><Typography variant="caption" color="text.secondary">Total Duration</Typography><Typography variant="h6">{formatDuration(summary.avgDurationMs * summary.totalCalls || 0)}</Typography></Grid>
            <Grid size={4}><Typography variant="caption" color="text.secondary">Avg Duration</Typography><Typography variant="h6">{summary.totalCalls > 0 ? formatDuration(summary.avgDurationMs) : '0s'}</Typography></Grid>
          </Grid>
        </Box>
      </Collapse>
    </Paper>
  );
}
