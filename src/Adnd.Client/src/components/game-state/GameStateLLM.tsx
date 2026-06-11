import { Box, Typography, Paper, Chip, Divider, Grid, Button } from '@mui/material';

interface GameStateLLMProps {
  gameState: any;
}

export default function GameStateLLM({ gameState }: GameStateLLMProps) {
  if (!gameState?.LLMStats) return <Typography>No LLM data available.</Typography>;
  const s = gameState.LLMStats;
  const successRate = s.TotalCalls > 0 ? (s.SuccessfulCalls / s.TotalCalls * 100) : 0;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography variant="h6">LLM Usage</Typography>

      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Total Calls" value={s.TotalCalls} />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Successful" value={s.SuccessfulCalls} sub={`${successRate.toFixed(1)}% success rate`} color="success" />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Failed" value={s.FailedCalls} sub={`${(100 - successRate).toFixed(1)}% failure rate`} color="error" />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Total Tokens" value={formatTokens(s.TotalTokens)} />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Prompt Tokens" value={formatTokens(s.TotalPromptTokens)} />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Completion Tokens" value={formatTokens(s.TotalCompletionTokens)} />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Avg Duration" value={`${Math.round(s.AvgDurationMs)}ms`} />
        </Box>
      </Box>

      {/* Provider Breakdown */}
      {gameState.LLMProviderBreakdown && gameState.LLMProviderBreakdown.length > 0 && (
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ mb: 1 }}>Provider Breakdown</Typography>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Provider</TableCell>
                  <TableCell>Model</TableCell>
                  <TableCell>Calls</TableCell>
                  <TableCell>Success Rate</TableCell>
                  <TableCell>Tokens</TableCell>
                  <TableCell>Avg Duration</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {gameState.LLMProviderBreakdown.map((p: any) => (
                  <TableRow key={p.providerType + p.model}>
                    <TableCell>{getProviderIcon(p.providerType)} {p.providerType}</TableCell>
                    <TableCell sx={{ fontFamily: 'monospace', fontSize: 11 }}>{p.model}</TableCell>
                    <TableCell>{p.totalCalls}</TableCell>
                    <TableCell>
                      <Chip label={`${(p.successRate * 100).toFixed(0)}%`} size="small"
                        color={p.successRate > 0.8 ? 'success' : p.successRate > 0.5 ? 'warning' : 'error'} />
                    </TableCell>
                    <TableCell>{formatTokens(p.totalTokens)}</TableCell>
                    <TableCell>{Math.round(p.avgDurationMs)}ms</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      )}
    </Box>
  );
}

// ==================== Triggers Tab ====================
