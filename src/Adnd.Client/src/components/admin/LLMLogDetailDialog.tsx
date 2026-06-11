import { Box, Typography, Dialog, DialogTitle, DialogContent, DialogActions, Button, Chip, Alert } from '@mui/material';

interface LLMLogDetailDialogProps {
  open: boolean;
  onClose: () => void;
  log: any;
}

export default function LLMLogDetailDialog({ open, onClose, log }: LLMLogDetailDialogProps) {
  if (!log) return null;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>LLM Interaction Details</DialogTitle>
      <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
          <Chip label={log.providerType} size="small" />
          <Chip label={log.model} size="small" />
          <Chip label={log.success ? 'Success' : 'Failed'} size="small" color={log.success ? 'success' : 'error'} />
          <Chip label={`${log.durationMs}ms`} size="small" />
        </Box>
        <Box>
          <Typography variant="subtitle2" color="text.secondary">Tokens</Typography>
          <Typography>Prompt: {log.promptTokens ?? '—'} | Completion: {log.completionTokens ?? '—'} | Total: {log.totalTokens ?? '—'}</Typography>
        </Box>
        <Box>
          <Typography variant="subtitle2" color="text.secondary">System Prompt</Typography>
          <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 150, overflow: 'auto' }}>
            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>{log.systemPrompt || '—'}</Typography>
          </Box>
        </Box>
        <Box>
          <Typography variant="subtitle2" color="text.secondary">User Prompt</Typography>
          <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 150, overflow: 'auto' }}>
            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>{log.userPrompt || '—'}</Typography>
          </Box>
        </Box>
        <Box>
          <Typography variant="subtitle2" color="text.secondary">Response</Typography>
          <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 200, overflow: 'auto' }}>
            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>{log.response || '—'}</Typography>
          </Box>
        </Box>
        <Box>
          <Typography variant="subtitle2" color="text.secondary">Origin</Typography>
          <Typography variant="body2">{log.origin}{log.originAgent ? ` (${log.originAgent})` : ''}{log.originAction ? ` → ${log.originAction}` : ''}</Typography>
        </Box>
        {log.error && (
          <Alert severity="error">{log.error}</Alert>
        )}
        <Typography variant="caption" color="text.secondary">
          {new Date(log.startedAt).toLocaleString()} → {new Date(log.completedAt).toLocaleString()}
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
