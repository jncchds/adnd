import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Chip, CircularProgress, Alert, IconButton, Collapse,
} from '@mui/material'
import { ExpandMore, ExpandLess } from '@mui/icons-material'
import { api } from '../api/client'
import type { AgentCall, AgentCallStatus } from '../types'

const STATUS_COLORS: Record<AgentCallStatus, 'default' | 'info' | 'success' | 'error' | 'warning'> = {
  Pending: 'info', Running: 'warning', Completed: 'success', Failed: 'error', Cancelled: 'default',
}

function CallRow({ call }: { call: AgentCall }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <TableRow sx={{ cursor: 'pointer' }} onClick={() => setOpen(o => !o)}>
        <TableCell><IconButton size="small">{open ? <ExpandLess /> : <ExpandMore />}</IconButton></TableCell>
        <TableCell><Chip label={call.status} size="small" color={STATUS_COLORS[call.status]} /></TableCell>
        <TableCell><Typography variant="caption">{call.action}</Typography></TableCell>
        <TableCell><Typography variant="caption">{call.fromAgent} → {call.toAgent}</Typography></TableCell>
        <TableCell><Typography variant="caption">{call.durationMs != null ? `${call.durationMs}ms` : '—'}</Typography></TableCell>
        <TableCell><Typography variant="caption">{new Date(call.createdAt).toLocaleString()}</Typography></TableCell>
      </TableRow>
      <TableRow>
        <TableCell colSpan={6} sx={{ py: 0 }}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <Box sx={{ p: 2 }}>
              {call.error && <Alert severity="error" sx={{ mb: 1 }}>{call.error}</Alert>}
              {call.outputMessage && (
                <Typography variant="body2" sx={{ mb: 1 }}>{call.outputMessage}</Typography>
              )}
              <Typography variant="caption" color="text.secondary">Step: {call.currentStep}</Typography>
            </Box>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  )
}

export default function AdminAgentCallsPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const [calls, setCalls] = useState<AgentCall[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!gameId) return
    setLoading(true)
    api.agentCalls.list(gameId)
      .then(setCalls)
      .catch(e => setError((e as Error).message))
      .finally(() => setLoading(false))
  }, [gameId])

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" fontWeight={700} sx={{ mb: 3 }}>Agent Calls</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell />
              <TableCell>Status</TableCell>
              <TableCell>Action</TableCell>
              <TableCell>Route</TableCell>
              <TableCell>Duration</TableCell>
              <TableCell>Time</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow><TableCell colSpan={6} align="center"><CircularProgress size={24} /></TableCell></TableRow>
            ) : calls.map(c => <CallRow key={c.id} call={c} />)}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  )
}
