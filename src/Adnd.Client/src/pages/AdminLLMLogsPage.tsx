import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, IconButton, Collapse, CircularProgress, Alert,
  Pagination, Button, Chip, Tooltip,
} from '@mui/material'
import { ExpandMore, ExpandLess, Delete as DeleteIcon, DeleteSweep } from '@mui/icons-material'
import { api } from '../api/client'
import type { LLMInteractionLog } from '../types'

const STATUS_COLOR: Record<LLMInteractionLog['status'], 'default' | 'warning' | 'success' | 'error'> = {
  Pending: 'default',
  Processing: 'warning',
  Completed: 'success',
  Failed: 'error',
}

function LogRow({ log, onDelete }: { log: LLMInteractionLog; onDelete: () => void }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <TableRow sx={{ '& > *': { borderBottom: 'unset' }, cursor: 'pointer' }} onClick={() => setOpen(o => !o)}>
        <TableCell>
          <IconButton size="small">{open ? <ExpandLess /> : <ExpandMore />}</IconButton>
        </TableCell>
        <TableCell><Typography variant="caption">{new Date(log.startedAt).toLocaleString()}</Typography></TableCell>
        <TableCell><Chip label={log.status} size="small" color={STATUS_COLOR[log.status]} /></TableCell>
        <TableCell><Chip label={log.model} size="small" /></TableCell>
        <TableCell><Typography variant="caption">{log.presetName}</Typography></TableCell>
        <TableCell align="right">
          <Typography variant="caption" component="span" color="text.secondary">{log.promptTokens.toLocaleString()}↑</Typography>
          {' '}
          <Typography variant="caption" component="span">{log.completionTokens.toLocaleString()}↓</Typography>
        </TableCell>
        <TableCell align="right">{log.durationMs}ms</TableCell>
        <TableCell>
          <Tooltip title="Delete">
            <IconButton size="small" onClick={e => { e.stopPropagation(); onDelete() }}>
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell colSpan={8} sx={{ py: 0 }}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1 }}>
              <Box>
                <Typography variant="caption" color="text.secondary" fontWeight={600}>System Prompt</Typography>
                <Paper sx={{ p: 1, mt: 0.5, bgcolor: 'action.hover', maxHeight: 200, overflow: 'auto' }}>
                  <Typography variant="caption" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                    {log.systemPrompt}
                  </Typography>
                </Paper>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary" fontWeight={600}>User Prompt</Typography>
                <Paper sx={{ p: 1, mt: 0.5, bgcolor: 'action.hover', maxHeight: 200, overflow: 'auto' }}>
                  <Typography variant="caption" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                    {log.userPrompt}
                  </Typography>
                </Paper>
              </Box>
              {log.reasoning && (
                <Box>
                  <Typography variant="caption" color="text.secondary" fontWeight={600}>Reasoning</Typography>
                  <Paper sx={{ p: 1, mt: 0.5, bgcolor: 'action.hover', maxHeight: 300, overflow: 'auto' }}>
                    <Typography variant="caption" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontStyle: 'italic', color: 'text.secondary' }}>
                      {log.reasoning}
                    </Typography>
                  </Paper>
                </Box>
              )}
              {log.status === 'Failed' && log.errorMessage && (
                <Box>
                  <Typography variant="caption" color="error" fontWeight={600}>Error</Typography>
                  <Paper sx={{ p: 1, mt: 0.5, bgcolor: 'rgba(211,47,47,0.08)', maxHeight: 200, overflow: 'auto' }}>
                    <Typography variant="caption" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                      {log.errorMessage}
                    </Typography>
                  </Paper>
                </Box>
              )}
              <Box>
                <Typography variant="caption" color="text.secondary" fontWeight={600}>Response</Typography>
                <Paper sx={{ p: 1, mt: 0.5, bgcolor: 'rgba(124,58,237,0.05)', maxHeight: 400, overflow: 'auto' }}>
                  <Typography variant="caption" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                    {log.response || (log.status === 'Processing' ? '(waiting for response…)' : '')}
                  </Typography>
                </Paper>
              </Box>
              <Typography variant="caption" color="text.secondary">
                {log.promptTokens.toLocaleString()} prompt + {log.completionTokens.toLocaleString()} completion = {log.totalTokens.toLocaleString()} total tokens
                {' · '}{log.durationMs}ms{' · '}{log.endpointUrl}
              </Typography>
            </Box>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  )
}

export default function AdminLLMLogsPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const [logs, setLogs] = useState<LLMInteractionLog[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = async (p: number, silent = false) => {
    if (!gameId) return
    if (!silent) setLoading(true)
    try {
      const data = await api.llmLogs.list(gameId, p)
      setLogs(data.items)
      setTotal(data.total)
    } catch (e) { setError((e as Error).message) }
    finally { if (!silent) setLoading(false) }
  }

  useEffect(() => { load(page) }, [page]) // eslint-disable-line react-hooks/exhaustive-deps

  // Poll while a call is still in flight so Pending/Processing rows resolve without a manual refresh.
  const hasInFlight = logs.some(l => l.status === 'Pending' || l.status === 'Processing')
  useEffect(() => {
    if (!hasInFlight) return
    const timer = setInterval(() => load(page, true), 3000)
    return () => clearInterval(timer)
  }, [hasInFlight, page]) // eslint-disable-line react-hooks/exhaustive-deps

  const handleDelete = async (id: string) => {
    try { await api.llmLogs.delete(id); load(page) }
    catch (e) { setError((e as Error).message) }
  }

  const handleDeleteAll = async () => {
    if (!gameId || !confirm(`Delete all LLM logs for this game?`)) return
    try { await api.llmLogs.deleteByGame(gameId); load(1) }
    catch (e) { setError((e as Error).message) }
  }

  const pageCount = Math.ceil(total / 20)

  return (
    <Box sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" fontWeight={700}>LLM Logs <Typography component="span" variant="body2" color="text.secondary">({total})</Typography></Typography>
        <Button color="error" startIcon={<DeleteSweep />} onClick={handleDeleteAll}>Delete All</Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell />
              <TableCell>Time</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Model</TableCell>
              <TableCell>Preset</TableCell>
              <TableCell align="right">In↑ Out↓</TableCell>
              <TableCell align="right">Duration</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow><TableCell colSpan={8} align="center"><CircularProgress size={24} /></TableCell></TableRow>
            ) : logs.map(log => (
              <LogRow key={log.id} log={log} onDelete={() => handleDelete(log.id)} />
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {pageCount > 1 && (
        <Box sx={{ mt: 2, display: 'flex', justifyContent: 'center' }}>
          <Pagination count={pageCount} page={page} onChange={(_, p) => setPage(p)} />
        </Box>
      )}
    </Box>
  )
}
