import { useState, useMemo } from 'react';
import { Box, Typography, Paper, Table, TableContainer, TableHead, TableCell, TableRow, TableBody, Alert, Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField, Chip, IconButton, CircularProgress } from '@mui/material';
import { Refresh as RefreshIcon, Delete as DeleteIcon } from '@mui/icons-material';
import { useLLMInteractions } from '../../api/hooks/useLLM';

function formatTokens(tokens: number): string {
  if (!tokens) return '0';
  if (tokens >= 1000000) return `${(tokens / 1000000).toFixed(1)}M`;
  if (tokens >= 1000) return `${(tokens / 1000).toFixed(1)}k`;
  return tokens.toString();
}

function formatDuration(seconds: number): string {
  if (!seconds) return '0s';
  if (seconds >= 3600) return `${(seconds / 3600).toFixed(1)}h`;
  if (seconds >= 60) return `${(seconds / 60).toFixed(1)}m`;
  return `${seconds}s`;
}

interface LLMLogsTabProps {
  onError: (msg: string) => void;
  onSuccess: (msg: string) => void;
}

export default function LLMLogsTab({ onError, onSuccess }: LLMLogsTabProps) {
  const [selectedLog, setSelectedLog] = useState<any>(null);
  const [showDetail, setShowDetail] = useState(false);
  const [filterProvider, setFilterProvider] = useState('');
  const [filterFrom, setFilterFrom] = useState('');
  const [filterTo, setFilterTo] = useState('');

  const { logs, isLoading, fetchLogs: refetchLogs } = useLLMInteractions();

  const providers = useMemo(() => {
    const provSet = new Set(logs.map((l: any) => l.providerType));
    return Array.from(provSet);
  }, [logs]);

  const handleRefresh = async () => {
    const params: any = { limit: 200 };
    if (filterProvider) params.providerType = filterProvider;
    if (filterFrom) params.from = filterFrom;
    if (filterTo) params.to = filterTo;
    await refetchLogs(params);
  };

  const handleDeleteLog = async (logId: string) => {
    try {
      await (window as any).api.deleteLLMInteraction(logId);
      onSuccess('Log deleted.');
      const params: any = { limit: 200 };
      if (filterProvider) params.providerType = filterProvider;
      if (filterFrom) params.from = filterFrom;
      if (filterTo) params.to = filterTo;
      await refetchLogs(params);
    } catch (e: any) {
      onError(e.message);
    }
  };

  const handleOpenDetail = (log: any) => {
    setSelectedLog(log);
    setShowDetail(true);
  };

  return (
    <Box>
      {/* Filters */}
      <Paper sx={{ p: 2, mb: 2, borderRadius: 2 }}>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <TextField
            size="small"
            label="Provider"
            select
            value={filterProvider}
            onChange={e => setFilterProvider(e.target.value)}
            SelectProps={{ native: true }}
            sx={{ minWidth: 140 }}
          >
            <option value="">All Providers</option>
            {providers.map(p => <option key={p} value={p}>{p.toUpperCase()}</option>)}
          </TextField>
          <TextField
            size="small"
            label="From"
            type="date"
            value={filterFrom}
            onChange={e => setFilterFrom(e.target.value)}
            InputLabelProps={{ shrink: true }}
            sx={{ minWidth: 140 }}
          />
          <TextField
            size="small"
            label="To"
            type="date"
            value={filterTo}
            onChange={e => setFilterTo(e.target.value)}
            InputLabelProps={{ shrink: true }}
            sx={{ minWidth: 140 }}
          />
          <Button variant="outlined" onClick={handleRefresh} startIcon={<RefreshIcon />}>
            Refresh
          </Button>
        </Box>
      </Paper>

      {/* Logs Table */}
      {isLoading ? (
        <Box sx={{ textAlign: 'center', py: 4 }}>
          <CircularProgress />
          <Typography sx={{ mt: 1, color: 'text.secondary' }}>Loading logs...</Typography>
        </Box>
      ) : logs.length === 0 ? (
        <Paper sx={{ p: 4, textAlign: 'center', bgcolor: 'background.default' }}>
          <Typography variant="body2" color="text.secondary">
            No LLM interaction logs yet. Logs will appear here once you make LLM calls.
          </Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 1 }}>
          <Table size="small">
            <TableHead>
              <TableRow sx={{ bgcolor: 'action.hover' }}>
                <TableCell sx={{ fontWeight: 600 }}>Time</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Preset</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Provider</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Model</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Tokens</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Duration</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Status</TableCell>
                <TableCell align="center" sx={{ fontWeight: 600 }}>Origin</TableCell>
                <TableCell sx={{ width: 56 }} />
              </TableRow>
            </TableHead>
            <TableBody>
              {logs.map((log, i) => (
                <TableRow
                  key={i}
                  sx={{ '&:hover': { bgcolor: 'action.hover' }, cursor: 'pointer' }}
                  onClick={() => handleOpenDetail(log)}
                >
                  <TableCell>
                    <Typography variant="body2" color="text.secondary">
                      {new Date(log.startedAt).toLocaleString()}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" fontWeight={500}>
                      {log.presetName || '—'}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Chip label={log.providerType.toUpperCase()} size="small" variant="outlined" />
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontFamily: 'monospace', fontSize: 12 }}>
                      {log.model}
                    </Typography>
                  </TableCell>
                  <TableCell align="center">
                    <Typography variant="body2" color="text.secondary">
                      {formatTokens(log.totalTokens || 0)}
                    </Typography>
                  </TableCell>
                  <TableCell align="center">
                    <Typography variant="body2" color="text.secondary">
                      {formatDuration(log.durationMs)}
                    </Typography>
                  </TableCell>
                  <TableCell align="center">
                    <Chip
                      label={log.success ? 'OK' : 'Fail'}
                      size="small"
                      color={log.success ? 'success' : 'error'}
                      variant="outlined"
                    />
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" color="text.secondary" sx={{ fontSize: 11 }}>
                      {log.origin}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={e => { e.stopPropagation(); handleDeleteLog(log.id); }} color="error">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Detail Dialog */}
      <Dialog open={showDetail} onClose={() => setShowDetail(false)} maxWidth="md" fullWidth>
        <DialogTitle>LLM Interaction Details</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          {selectedLog && (
            <>
              <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                <Chip label={selectedLog.presetName || '—'} size="small" />
                <Chip label={selectedLog.providerType.toUpperCase()} size="small" variant="outlined" />
                <Chip label={selectedLog.model} size="small" />
                <Chip
                  label={selectedLog.success ? 'Success' : 'Failed'}
                  size="small"
                  color={selectedLog.success ? 'success' : 'error'}
                />
                <Chip label={`${selectedLog.durationMs}ms`} size="small" />
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">Tokens</Typography>
                <Typography>
                  Prompt: {selectedLog.promptTokens ?? '—'} | Completion: {selectedLog.completionTokens ?? '—'} | Total: {selectedLog.totalTokens ?? '—'}
                </Typography>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">System Prompt</Typography>
                <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 150, overflow: 'auto' }}>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>
                    {selectedLog.systemPrompt || '—'}
                  </Typography>
                </Box>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">User Prompt</Typography>
                <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 150, overflow: 'auto' }}>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>
                    {selectedLog.userPrompt || '—'}
                  </Typography>
                </Box>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">Response</Typography>
                <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 200, overflow: 'auto' }}>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>
                    {selectedLog.response || '—'}
                  </Typography>
                </Box>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">Origin</Typography>
                <Typography variant="body2">
                  {selectedLog.origin}
                  {selectedLog.originAgent ? ` (${selectedLog.originAgent})` : ''}
                  {selectedLog.originAction ? ` → ${selectedLog.originAction}` : ''}
                </Typography>
              </Box>
              {selectedLog.error && (
                <Alert severity="error">{selectedLog.error}</Alert>
              )}
              <Typography variant="caption" color="text.secondary">
                {new Date(selectedLog.startedAt).toLocaleString()} → {new Date(selectedLog.completedAt).toLocaleString()}
              </Typography>
            </>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowDetail(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

// ==================== Helpers ====================

