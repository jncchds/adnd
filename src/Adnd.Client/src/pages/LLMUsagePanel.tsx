import { useState } from 'react';
import { Box, Card, CardContent, Typography, Chip, Grid, Paper, Button } from '@mui/material';
import { useGameProviderUsage } from '../api/hooks/useLLM';
import { usePendingCalls } from '../api/hooks/useAgent';
import { SummaryCard } from '../components/admin/LLMUsageComponents';
import ProviderRow, { formatTokens } from '../components/admin/LLMUsageComponents';

interface LLMUsagePanelProps {
  gameId: string | undefined;
  compact?: boolean;
}

export default function LLMUsagePanel({ gameId, compact = false }: LLMUsagePanelProps) {
  const { usage, isLoading: _, error, refetch } = useGameProviderUsage(gameId);
  const { calls: _pendingCalls } = usePendingCalls(gameId);
  const [dateFilter, setDateFilter] = useState<'all' | '7d' | '30d' | '90d'>('all');

  const totalCalls = usage.reduce((sum, u) => sum + u.totalCalls, 0);
  const totalSuccess = usage.reduce((sum, u) => sum + u.successfulCalls, 0);
  const totalFailed = usage.reduce((sum, u) => sum + u.failedCalls, 0);
  const totalTokens = usage.reduce((sum, u) => sum + u.totalTokens, 0);
  const successRate = totalCalls > 0 ? Math.round((totalSuccess / totalCalls) * 100) : 0;

  const handleDateFilter = (filter: 'all' | '7d' | '30d' | '90d') => {
    setDateFilter(filter);
    if (filter === 'all') { refetch(); }
    else {
      const days = filter === '7d' ? 7 : filter === '30d' ? 30 : 90;
      const from = new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString();
      refetch(from);
    }
  };

  if (compact) {
    return (
      <Box>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
          <Typography variant="subtitle2" fontWeight={600}>LLM Usage</Typography>
          <Box sx={{ display: 'flex', gap: 0.5 }}>
            {(['7d', '30d'] as const).map(f => (
              <Chip key={f} label={f} size="small" clickable onClick={() => handleDateFilter(f)} color={dateFilter === f ? 'primary' : 'default'} variant={dateFilter === f ? 'filled' : 'outlined'} />
            ))}
          </Box>
        </Box>
        <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
          <Typography variant="body2" fontWeight={600}>{totalCalls} calls</Typography>
          <Typography variant="body2" color="text.secondary">{formatTokens(totalTokens)} tokens</Typography>
          <Chip label={`${successRate}% success`} size="small" color={successRate >= 90 ? 'success' : successRate >= 70 ? 'warning' : 'error'} />
        </Box>
        {usage.length > 0 && (
          <Box sx={{ mt: 1 }}>
            {usage.map((u, i) => (
              <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                <Chip label={u.providerType.toUpperCase()} size="small" variant="outlined" sx={{ fontSize: '0.65rem', height: 18 }} />
                <Typography variant="caption" color="text.secondary">{u.model}</Typography>
                <Typography variant="caption" color="text.secondary">{u.totalCalls} calls</Typography>
              </Box>
            ))}
          </Box>
        )}
        {error && <Typography variant="caption" color="error" sx={{ mt: 1, display: 'block' }}>{error}</Typography>}
      </Box>
    );
  }

  return (
    <Card sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 3 }}>
        <Typography variant="h6" fontWeight={700} sx={{ mb: 1 }}>LLM Provider Usage</Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 2 }}>Track usage across all providers and models for cost planning</Typography>
        <Grid container spacing={2} sx={{ mb: 3 }}>
          <Grid size={{ xs: 6, md: 3 }}><SummaryCard label="Total Calls" value={totalCalls.toString()} color="primary" icon="📞" /></Grid>
          <Grid size={{ xs: 6, md: 3 }}><SummaryCard label="Successful" value={totalSuccess.toString()} color="success" sublabel={`${totalFailed} failed`} icon="✅" /></Grid>
          <Grid size={{ xs: 6, md: 3 }}><SummaryCard label="Total Tokens" value={formatTokens(totalTokens)} color="primary" sublabel={`${formatTokens(totalTokens - (totalTokens - totalTokens))} prompt`} icon="🔤" /></Grid>
          <Grid size={{ xs: 6, md: 3 }}><SummaryCard label="Success Rate" value={`${successRate}%`} color={successRate >= 90 ? 'success' : successRate >= 70 ? 'warning' : 'error'} icon="📊" /></Grid>
        </Grid>
        <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
          <Button size="small" variant={dateFilter === 'all' ? 'contained' : 'outlined'} onClick={() => handleDateFilter('all')}>All Time</Button>
          <Button size="small" variant={dateFilter === '7d' ? 'contained' : 'outlined'} onClick={() => handleDateFilter('7d')}>7 Days</Button>
          <Button size="small" variant={dateFilter === '30d' ? 'contained' : 'outlined'} onClick={() => handleDateFilter('30d')}>30 Days</Button>
          <Button size="small" variant={dateFilter === '90d' ? 'contained' : 'outlined'} onClick={() => handleDateFilter('90d')}>90 Days</Button>
        </Box>
        {usage.length === 0 ? (
          <Paper sx={{ p: 4, textAlign: 'center' }}><Typography color="text.secondary">No LLM usage data available</Typography></Paper>
        ) : (
          usage.map((u, i) => <ProviderRow key={i} summary={u} />)
        )}
        {error && <Typography variant="caption" color="error" sx={{ mt: 1, display: 'block' }}>Error: {error}</Typography>}
      </CardContent>
    </Card>
  );
}
