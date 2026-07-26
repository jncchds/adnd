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

function LogRow({ log, onDelete }: { log: LLMInteractionLog; onDelete: () => void }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <TableRow sx={{ '& > *': { borderBottom: 'unset' }, cursor: 'pointer' }} onClick={() => setOpen(o => !o)}>
        <TableCell>
          <IconButton size="small">{open ? <ExpandLess /> : <ExpandMore />}</IconButton>
        </TableCell>
        <TableCell><Typography variant="caption">{new Date(log.startedAt).toLocaleString()}</Typography></TableCell>
        <TableCell><Chip label={log.model} size="small" /></TableCell>
        <TableCell><Typography variant="caption">{log.presetName}</Typography></TableCell>
        <TableCell align="right">{log.totalTokens.toLocaleString()}</TableCell>
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
        <TableCell colSpan={7} sx={{ py: 0 }}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1 }}>
              <Box>
                <Typography variant="caption" color="text.secondary" fontWeight={600}>System Prompt</Typography>
                <Paper sx={{ p: 1, mt: 0.5, bgcolor: 'action.hover', maxHeight: 150, overflow: 'auto' }}>
                  <Typography variant="caption" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                    {log.systemPrompt}
                  </Typography>
                </Paper>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary" fontWeight={600}>User Prompt</Typography>
                <Paper sx={{ p: 1, mt: 0.5, bgcolor: 'action.hover', maxHeight: 100, overflow: 'auto' }}>
                  <Typography variant="caption" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                    {log.userPrompt}
                  </Typography>
                </Paper>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary" fontWeight={600}>Response</Typography>
                <Paper sx={{ p: 1, mt: 0.5, bgcolor: 'rgba(124,58,237,0.05)', maxHeight: 200, overflow: 'auto' }}>
                  <Typography variant="caption" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                    {log.response}
                  </Typography>
                </Paper>
              </Box>
              <Typography variant="caption" color="text.secondary">
                Tokens: {log.promptTokens} prompt + {log.completionTokens} completion = {log.totalTokens} total
                {' · '}{log.endpointUrl}
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

  const load = async (p: number) => {
    if (!gameId) return
    setLoading(true)
    try {
      const data = await api.llmLogs.list(gameId, p)
      setLogs(data.items)
      setTotal(data.total)
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { load(page) }, [page]) // eslint-disable-line react-hooks/exhaustive-deps

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
              <TableCell>Model</TableCell>
              <TableCell>Preset</TableCell>
              <TableCell align="right">Tokens</TableCell>
              <TableCell align="right">Duration</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow><TableCell colSpan={7} align="center"><CircularProgress size={24} /></TableCell></TableRow>
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
