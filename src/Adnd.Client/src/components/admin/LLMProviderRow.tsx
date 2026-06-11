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

function timeAgo(dateStr?: string): string {
  if (!dateStr) return 'now';
  const diff = Date.now() - new Date(dateStr).getTime();
  const secs = Math.floor(diff / 1000);
  if (secs < 60) return `${secs}s ago`;
  const mins = Math.floor(secs / 60);
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  return `${hours}h ago`;
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

