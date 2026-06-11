function LLMStatsTab() {
  const { usage, isLoading, refetch } = useUserLLMUsage();
  const [dateFilter, setDateFilter] = useState<'all' | '7d' | '30d' | '90d'>('all');

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

  // Compute totals
  const totalCalls = usage.reduce((s, u) => s + u.totalCalls, 0);
  const totalSuccess = usage.reduce((s, u) => s + u.successfulCalls, 0);
  const totalTokens = usage.reduce((s, u) => s + u.totalTokens, 0);
  const successRate = totalCalls > 0 ? Math.round((totalSuccess / totalCalls) * 100) : 0;

  if (isLoading) {
    return (
      <Box sx={{ textAlign: 'center', py: 8 }}>
        <CircularProgress />
        <Typography sx={{ mt: 2, color: 'text.secondary' }}>Loading statistics...</Typography>
      </Box>
    );
  }

  return (
    <Box>
      {/* Summary Cards */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 6, md: 3 }}>
          <SummaryCard label="Total Calls" value={totalCalls.toString()} icon="📊" />
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
          <SummaryCard label="Total Tokens" value={formatTokens(totalTokens)} icon="🔤" />
        </Grid>
        <Grid size={{ xs: 6, md: 3 }}>
          <SummaryCard label="Presets Used" value={usage.length.toString()} icon="🤖" />
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

      {/* Usage by Preset */}
      {usage.length === 0 ? (
        <Paper sx={{ p: 4, textAlign: 'center', bgcolor: 'background.default' }}>
          <Typography variant="body2" color="text.secondary">
            No LLM usage data yet. Interactions will appear here once you use an LLM preset.
          </Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 1 }}>
          <Table size="small">
            <TableHead>
              <TableRow sx={{ bgcolor: 'action.hover' }}>
                <TableCell sx={{ fontWeight: 600 }}>Preset</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Provider</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Calls</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Success</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Tokens</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Avg Time</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Last Used</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {usage.map((u, i) => {
                const sRate = u.totalCalls > 0 ? Math.round((u.successfulCalls / u.totalCalls) * 100) : 0;
                return (
                  <TableRow key={i} sx={{ '&:hover': { bgcolor: 'action.hover' } }}>
                    <TableCell>
                      <Typography variant="body2" fontWeight={600}>
                        {u.presetName || 'Unnamed'}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Chip label={u.providerType.toUpperCase()} size="small" variant="outlined" />
                    </TableCell>
                    <TableCell align="center">
                      <Typography variant="body2" fontWeight={600}>{u.totalCalls}</Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, justifyContent: 'center' }}>
                        <LinearProgress
                          variant="determinate"
                          value={sRate}
                          color={sRate >= 90 ? 'success' : sRate >= 70 ? 'warning' : 'error'}
                          sx={{ width: 50, height: 5, borderRadius: 3 }}
                        />
                        <Typography variant="body2" fontSize={12} color="text.secondary">
                          {sRate}%
                        </Typography>
                      </Box>
                    </TableCell>
                    <TableCell align="center">
                      <Typography variant="body2" color="text.secondary">
                        {formatTokens(u.totalTokens)}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Typography variant="body2" color="text.secondary">
                        {formatDuration(u.avgDurationMs)}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Typography variant="body2" color="text.secondary">
                        {new Date(u.lastUsed).toLocaleDateString()}
                      </Typography>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}

// ==================== LLM Logs Tab ====================

