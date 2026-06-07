import {
  Box,
  Card,
  CardContent,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  LinearProgress,
  Collapse,
  IconButton,
  Paper,
  Grid,
  Divider,
  Tooltip,
} from '@mui/material';
import { ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon } from '@mui/icons-material';
import { useState } from 'react';
import { useGameProviderUsage } from '../api/gameHooks';
import type { GameProviderUsageSummary } from '../types';

// ==================== Summary Card ====================

interface SummaryCardProps {
  label: string;
  value: string;
  sublabel?: string;
  color?: 'primary' | 'success' | 'warning' | 'error';
  icon?: string;
}

function SummaryCard({ label, value, sublabel, color = 'primary', icon }: SummaryCardProps) {
  const colorMap = {
    primary: 'success',
    success: 'success',
    warning: 'warning',
    error: 'error',
  };

  return (
    <Card sx={{ height: '100%', bgcolor: 'background.paper', borderRadius: 2 }}>
      <CardContent sx={{ p: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
          {icon && <Typography variant="h4" sx={{ fontSize: '1.5rem' }}>{icon}</Typography>}
          <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>
            {label}
          </Typography>
        </Box>
        <Typography variant="h4" fontWeight={700} color={colorMap[color]}>
          {value}
        </Typography>
        {sublabel && (
          <Typography variant="caption" color="text.secondary">
            {sublabel}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

// ==================== Provider Row ====================

interface ProviderRowProps {
  summary: GameProviderUsageSummary;
}

function formatTokens(tokens: number): string {
  if (tokens >= 1_000_000) return `${(tokens / 1_000_000).toFixed(1)}M`;
  if (tokens >= 1_000) return `${(tokens / 1_000).toFixed(1)}K`;
  return tokens.toString();
}

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  const secs = Math.floor(ms / 1000);
  if (secs < 60) return `${secs}s`;
  const mins = Math.floor(secs / 60);
  const remSecs = secs % 60;
  return `${mins}m ${remSecs}s`;
}

function ProviderRow({ summary }: ProviderRowProps) {
  const [expanded, setExpanded] = useState(false);

  const successRate = summary.totalCalls > 0
    ? Math.round((summary.successfulCalls / summary.totalCalls) * 100)
    : 0;

  const providerColor =
    summary.providerType === 'openai' ? 'success' :
    summary.providerType === 'google' ? 'info' :
    summary.providerType === 'ollama' ? 'primary' :
    summary.providerType === 'lmstudio' ? 'primary' :
    'default';

  return (
    <Box>
      <TableRow
        sx={{
          bgcolor: 'background.default',
          '&:hover': { bgcolor: 'action.hover' },
          cursor: 'pointer',
        }}
        onClick={() => setExpanded(!expanded)}
      >
        <TableCell sx={{ pl: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Chip
              label={summary.providerType.toUpperCase()}
              size="small"
              color={providerColor as any}
              variant="outlined"
              sx={{ fontWeight: 600, fontSize: '0.7rem' }}
            />
            <Typography variant="body2" fontWeight={600}>
              {summary.model}
            </Typography>
          </Box>
        </TableCell>
        <TableCell align="center">
          <Typography variant="body2" fontWeight={600}>{summary.totalCalls}</Typography>
        </TableCell>
        <TableCell align="center">
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, justifyContent: 'center' }}>
            <LinearProgress
              variant="determinate"
              value={successRate}
              color={successRate >= 90 ? 'success' : successRate >= 70 ? 'warning' : 'error'}
              sx={{ width: 60, height: 6, borderRadius: 3 }}
            />
            <Typography variant="body2" fontSize={12} color="text.secondary">
              {successRate}%
            </Typography>
          </Box>
        </TableCell>
        <TableCell align="center">
          <Typography variant="body2" color="text.secondary">
            {formatTokens(summary.totalTokens)}
          </Typography>
        </TableCell>
        <TableCell align="center">
          <Typography variant="body2" color="text.secondary">
            {formatDuration(summary.avgDurationMs)}
          </Typography>
        </TableCell>
        <TableCell align="center">
          <Typography variant="body2" color="text.secondary">
            {new Date(summary.firstCall).toLocaleDateString()}
          </Typography>
        </TableCell>
        <TableCell align="right" sx={{ pr: 2 }}>
          <IconButton size="small" onClick={(e) => { e.stopPropagation(); setExpanded(!expanded); }}>
            {expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
          </IconButton>
        </TableCell>
      </TableRow>

      <TableRow>
        <TableCell colSpan={8} sx={{ p: 0 }}>
          <Collapse in={expanded} timeout="auto" unmountOnExit>
            <Box sx={{ p: 2, bgcolor: 'background.paper', borderRadius: '0 0 8px 8px' }}>
              <Grid container spacing={2}>
                <Grid size={{ xs: 12, md: 4 }}>
                  <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase' }}>
                    Prompt Tokens
                  </Typography>
                  <Typography variant="body1" fontWeight={600}>
                    {formatTokens(summary.totalPromptTokens)}
                  </Typography>
                </Grid>
                <Grid size={{ xs: 12, md: 4 }}>
                  <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase' }}>
                    Completion Tokens
                  </Typography>
                  <Typography variant="body1" fontWeight={600}>
                    {formatTokens(summary.totalCompletionTokens)}
                  </Typography>
                </Grid>
                <Grid size={{ xs: 12, md: 4 }}>
                  <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase' }}>
                    Max Duration
                  </Typography>
                  <Typography variant="body1" fontWeight={600}>
                    {formatDuration(summary.maxDurationMs)}
                  </Typography>
                </Grid>
                <Grid size={{ xs: 12, md: 6 }}>
                  <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase' }}>
                    First Call
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {new Date(summary.firstCall).toLocaleString()}
                  </Typography>
                </Grid>
                <Grid size={{ xs: 12, md: 6 }}>
                  <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase' }}>
                    Last Call
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {new Date(summary.lastCall).toLocaleString()}
                  </Typography>
                </Grid>
                <Grid size={12}>
                  <Divider sx={{ my: 1 }} />
                  <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                    <Chip
                      label={`${summary.successfulCalls} successful`}
                      color="success"
                      size="small"
                      variant="outlined"
                    />
                    <Chip
                      label={`${summary.failedCalls} failed`}
                      color="error"
                      size="small"
                      variant="outlined"
                    />
                    <Tooltip title={`${(summary.successRate * 100).toFixed(1)}% success rate`}>
                      <Chip
                        label={`${(summary.successRate * 100).toFixed(1)}% success`}
                        color={summary.successRate >= 0.9 ? 'success' : summary.successRate >= 0.7 ? 'warning' : 'error'}
                        size="small"
                      />
                    </Tooltip>
                  </Box>
                </Grid>
              </Grid>
            </Box>
          </Collapse>
        </TableCell>
      </TableRow>
    </Box>
  );
}

// ==================== Main Panel ====================

interface LLMUsagePanelProps {
  gameId: string;
  compact?: boolean;
}

export default function LLMUsagePanel({ gameId, compact = false }: LLMUsagePanelProps) {
  const { usage, isLoading, error, refetch } = useGameProviderUsage(gameId);
  const [dateFilter, setDateFilter] = useState<'all' | '7d' | '30d' | '90d'>('all');

  // Compute totals across all providers
  const totalCalls = usage.reduce((sum, u) => sum + u.totalCalls, 0);
  const totalSuccess = usage.reduce((sum, u) => sum + u.successfulCalls, 0);
  const totalFailed = usage.reduce((sum, u) => sum + u.failedCalls, 0);
  const totalTokens = usage.reduce((sum, u) => sum + u.totalTokens, 0);
  const successRate = totalCalls > 0 ? Math.round((totalSuccess / totalCalls) * 100) : 0;

  const handleDateFilter = (filter: 'all' | '7d' | '30d' | '90d') => {
    setDateFilter(filter);
    if (filter === 'all') {
      refetch();
    } else {
      const days = filter === '7d' ? 7 : filter === '30d' ? 30 : 90;
      const from = new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString();
      refetch(from);
    }
  };

  if (compact) {
    // Compact summary view
    return (
      <Box>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
          <Typography variant="subtitle2" fontWeight={600}>
            LLM Usage
          </Typography>
          <Box sx={{ display: 'flex', gap: 0.5 }}>
            {(['7d', '30d'] as const).map(f => (
              <Chip
                key={f}
                label={f}
                size="small"
                clickable
                onClick={() => handleDateFilter(f)}
                color={dateFilter === f ? 'primary' : 'default'}
                variant={dateFilter === f ? 'filled' : 'outlined'}
              />
            ))}
          </Box>
        </Box>
        <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
          <Typography variant="body2" fontWeight={600} color="text.primary">
            {totalCalls} calls
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {formatTokens(totalTokens)} tokens
          </Typography>
          <Chip
            label={`${successRate}% success`}
            size="small"
            color={successRate >= 90 ? 'success' : successRate >= 70 ? 'warning' : 'error'}
          />
        </Box>
        {usage.length > 0 && (
          <Box sx={{ mt: 1 }}>
            {usage.map((u, i) => (
              <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                <Chip label={u.providerType.toUpperCase()} size="small" variant="outlined" sx={{ fontSize: '0.65rem', height: 18 }} />
                <Typography variant="caption" color="text.secondary">
                  {u.model}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {u.totalCalls} calls
                </Typography>
              </Box>
            ))}
          </Box>
        )}
        {error && (
          <Typography variant="caption" color="error" sx={{ mt: 1, display: 'block' }}>
            {error}
          </Typography>
        )}
      </Box>
    );
  }

  // Full panel view
  return (
    <Card sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 3 }}>
        <Typography variant="h6" fontWeight={700} sx={{ mb: 1 }}>
          LLM Provider Usage
        </Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 2 }}>
          Track usage across all providers and models for cost planning
        </Typography>

        {/* Summary Cards */}
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid size={{ xs: 6, md: 3 }}>
            <SummaryCard
              label="Total Calls"
              value={totalCalls.toString()}
              color="primary"
              icon="📊"
            />
          </Grid>
          <Grid size={{ xs: 6, md: 3 }}>
            <SummaryCard
              label="Success Rate"
              value={`${successRate}%`}
              sublabel={`${totalSuccess}/${totalCalls}`}
              color={successRate >= 90 ? 'success' : successRate >= 70 ? 'warning' : 'error'}
              icon="✅"
            />
          </Grid>
          <Grid size={{ xs: 6, md: 3 }}>
            <SummaryCard
              label="Total Tokens"
              value={formatTokens(totalTokens)}
              sublabel={`${formatTokens(totalTokens - totalFailed * 100)} used`}
              color="primary"
              icon="🔤"
            />
          </Grid>
          <Grid size={{ xs: 6, md: 3 }}>
            <SummaryCard
              label="Providers"
              value={usage.length.toString()}
              sublabel={`${usage.filter(u => u.failedCalls > 0).length} with errors`}
              color={usage.filter(u => u.failedCalls > 0).length > 0 ? 'warning' : 'success'}
              icon="🤖"
            />
          </Grid>
        </Grid>

        {/* Date Filter */}
        <Box sx={{ display: 'flex', gap: 0.5, mb: 2 }}>
          {(['all', '7d', '30d', '90d'] as const).map(f => (
            <Chip
              key={f}
              label={f === 'all' ? 'All time' : f}
              size="small"
              clickable
              onClick={() => handleDateFilter(f)}
              color={dateFilter === f ? 'primary' : 'default'}
              variant={dateFilter === f ? 'filled' : 'outlined'}
            />
          ))}
        </Box>

        {/* Provider Table */}
        {isLoading ? (
          <LinearProgress sx={{ mb: 2 }} />
        ) : error ? (
          <Typography color="error" variant="body2" sx={{ textAlign: 'center', py: 2 }}>
            {error}
          </Typography>
        ) : usage.length === 0 ? (
          <Paper sx={{ p: 4, textAlign: 'center', bgcolor: 'background.default' }}>
            <Typography variant="body2" color="text.secondary">
              No LLM usage data yet. Interactions will appear here once the game starts.
            </Typography>
          </Paper>
        ) : (
          <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 1 }}>
            <Table size="small">
              <TableHead>
                <TableRow sx={{ bgcolor: 'action.hover' }}>
                  <TableCell sx={{ fontWeight: 600 }}>Provider / Model</TableCell>
                  <TableCell align="center" sx={{ fontWeight: 600 }}>Calls</TableCell>
                  <TableCell align="center" sx={{ fontWeight: 600 }}>Success</TableCell>
                  <TableCell align="center" sx={{ fontWeight: 600 }}>Tokens</TableCell>
                  <TableCell align="center" sx={{ fontWeight: 600 }}>Avg Time</TableCell>
                  <TableCell align="center" sx={{ fontWeight: 600 }}>First Used</TableCell>
                  <TableCell sx={{ width: 36 }} />
                </TableRow>
              </TableHead>
              <TableBody>
                {usage.map((u, i) => (
                  <ProviderRow key={i} summary={u} />
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </CardContent>
    </Card>
  );
}
