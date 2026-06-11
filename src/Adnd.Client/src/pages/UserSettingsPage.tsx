import { useState, useMemo } from 'react';
import { useAuth } from '../api/hooks/useAuth';
import { useUserLLMUsage, useLLMInteractions } from '../api/hooks/useLLM';
import {
  Box,
  Typography,
  Paper,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  IconButton,
  Chip,
  Alert,
  AlertTitle,
  InputAdornment,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  LinearProgress,
  Card,
  CardContent,
  Grid,
  Tab,
  Tabs,
  CircularProgress,
} from '@mui/material';
import {
  Visibility as EyeIcon,
  VisibilityOff as EyeOffIcon,
  CheckCircle as CheckIcon,
  Refresh as RefreshIcon,
  Delete as DeleteIcon,
  History as HistoryIcon,
  BarChart as BarChartIcon,
  Lock as LockIcon,
} from '@mui/icons-material';

export default function UserSettingsPage() {
  const { refreshUser } = useAuth();
  const [activeTab, setActiveTab] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const handleSuccess = (msg: string) => {
    setSuccess(msg);
    setTimeout(() => setSuccess(null), 3000);
  };

  return (
    <Box>
      {/* Title */}
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>
        User Settings
      </Typography>

      {/* Messages */}
      {success && (
        <Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2, alignItems: 'center' }}>
          <CheckIcon fontSize="small" sx={{ mr: 1 }} />
          {success}
        </Alert>
      )}
      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {error}
        </Alert>
      )}

      {/* Tabs */}
      <Paper sx={{ mb: 3 }}>
        <Tabs
          value={activeTab}
          onChange={(_, v) => setActiveTab(v)}
          sx={{ borderBottom: 1, borderColor: 'divider', px: 2 }}
        >
          <Tab icon={<LockIcon fontSize="small" />} label="Password" sx={{ textTransform: 'none', minWidth: 120 }} />
          <Tab icon={<BarChartIcon fontSize="small" />} label="LLM Statistics" sx={{ textTransform: 'none', minWidth: 120 }} />
          <Tab icon={<HistoryIcon fontSize="small" />} label="LLM Logs" sx={{ textTransform: 'none', minWidth: 120 }} />
        </Tabs>
      </Paper>

      {/* Tab Content */}
      {activeTab === 0 && <PasswordTab onError={setError} onSuccess={handleSuccess} onRefreshUser={refreshUser} />}
      {activeTab === 1 && <LLMStatsTab />}
      {activeTab === 2 && <LLMLogsTab onError={setError} onSuccess={handleSuccess} />}
    </Box>
  );
}

// ==================== Password Tab ====================

interface PasswordTabProps {
  onError: (msg: string) => void;
  onSuccess: (msg: string) => void;
  onRefreshUser: () => Promise<void>;
}

function PasswordTab({ onError, onSuccess, onRefreshUser }: PasswordTabProps) {
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleChangePassword = async () => {
    if (!currentPassword || !newPassword || !confirmPassword) {
      onError('All fields are required.');
      return;
    }
    if (newPassword !== confirmPassword) {
      onError('New passwords do not match.');
      return;
    }
    if (newPassword.length < 8) {
      onError('New password must be at least 8 characters.');
      return;
    }
    setLoading(true);
    try {
      await (window as any).api.changePassword(currentPassword, newPassword);
      onSuccess('Password changed successfully!');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      await onRefreshUser();
    } catch (e: any) {
      onError(e.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Paper sx={{ p: 3, borderRadius: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 3 }}>
        <LockIcon color="primary" />
        <Typography variant="h6" fontWeight={600}>Change Password</Typography>
      </Box>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 400 }}>
        <TextField
          fullWidth
          label="Current Password"
          type={showCurrent ? 'text' : 'password'}
          value={currentPassword}
          onChange={e => setCurrentPassword(e.target.value)}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton size="small" onClick={() => setShowCurrent(!showCurrent)}>
                  {showCurrent ? <EyeOffIcon fontSize="small" /> : <EyeIcon fontSize="small" />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />
        <TextField
          fullWidth
          label="New Password"
          type={showNew ? 'text' : 'password'}
          value={newPassword}
          onChange={e => setNewPassword(e.target.value)}
          helperText={newPassword.length >= 8 ? 'Minimum 8 characters' : `${8 - newPassword.length} more characters needed`}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton size="small" onClick={() => setShowNew(!showNew)}>
                  {showNew ? <EyeOffIcon fontSize="small" /> : <EyeIcon fontSize="small" />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />
        <TextField
          fullWidth
          label="Confirm New Password"
          type={showConfirm ? 'text' : 'password'}
          value={confirmPassword}
          onChange={e => setConfirmPassword(e.target.value)}
          helperText={confirmPassword && newPassword !== confirmPassword ? 'Passwords do not match' : ''}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton size="small" onClick={() => setShowConfirm(!showConfirm)}>
                  {showConfirm ? <EyeOffIcon fontSize="small" /> : <EyeIcon fontSize="small" />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />
        <Button
          variant="contained"
          onClick={handleChangePassword}
          disabled={loading || !currentPassword || !newPassword || !confirmPassword}
        >
          {loading ? <CircularProgress size={24} /> : 'Change Password'}
        </Button>
      </Box>
    </Paper>
  );
}

// ==================== LLM Statistics Tab ====================

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

interface LLMLogsTabProps {
  onError: (msg: string) => void;
  onSuccess: (msg: string) => void;
}

function LLMLogsTab({ onError, onSuccess }: LLMLogsTabProps) {
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

function SummaryCard({ label, value, sublabel, color = 'primary', icon }: {
  label: string; value: string; sublabel?: string; color?: string; icon?: string;
}) {
  return (
    <Card sx={{ height: '100%', bgcolor: 'background.paper', borderRadius: 2 }}>
      <CardContent sx={{ p: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
          {icon && <Typography variant="h4" sx={{ fontSize: '1.5rem' }}>{icon}</Typography>}
          <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>
            {label}
          </Typography>
        </Box>
        <Typography variant="h4" fontWeight={700} color={color}>
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
