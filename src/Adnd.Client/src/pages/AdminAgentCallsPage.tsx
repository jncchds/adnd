import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Chip, CircularProgress, Alert, IconButton, Collapse,
} from '@mui/material'
import { ExpandMore, ExpandLess } from '@mui/icons-material'
import { api } from '../api/client'
import type { AgentCall, AgentCallStatus, GMToolCallSummary, GMToolCallStatus } from '../types'

const STATUS_COLORS: Record<AgentCallStatus, 'default' | 'info' | 'success' | 'error' | 'warning'> = {
  Pending: 'info', Running: 'warning', Completed: 'success', Failed: 'error', Cancelled: 'default',
}

const TOOL_STATUS_COLORS: Record<GMToolCallStatus, 'default' | 'info' | 'success' | 'error' | 'warning'> = {
  Pending: 'info', Running: 'warning', Completed: 'success', Failed: 'error',
  AwaitingConfirmation: 'warning', Declined: 'default',
}

function ToolCallList({ agentCallId }: { agentCallId: string }) {
  const [toolCalls, setToolCalls] = useState<GMToolCallSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.toolCalls.byAgentCall(agentCallId)
      .then(setToolCalls)
      .catch(e => setError((e as Error).message))
  }, [agentCallId])

  if (error) return <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>
  if (toolCalls === null) return <CircularProgress size={16} />
  if (toolCalls.length === 0) return <Typography variant="caption" color="text.secondary">No tool calls.</Typography>

  return (
    <Table size="small">
      <TableBody>
        {toolCalls.map(t => {
          const durationMs = t.completedAt
            ? new Date(t.completedAt).getTime() - new Date(t.startedAt).getTime()
            : null
          return (
            <TableRow key={t.id}>
              <TableCell sx={{ border: 0, pl: 0 }}>
                <Chip label={t.status} size="small" color={TOOL_STATUS_COLORS[t.status]} />
              </TableCell>
              <TableCell sx={{ border: 0 }}><Typography variant="caption">{t.toolName}</Typography></TableCell>
              <TableCell sx={{ border: 0 }}>
                {t.requiresConfirmation && <Chip label="confirm" size="small" variant="outlined" />}
              </TableCell>
              <TableCell sx={{ border: 0 }} align="right">
                <Typography variant="caption" color="text.secondary">
                  {durationMs != null ? `${durationMs}ms` : '—'}
                </Typography>
              </TableCell>
            </TableRow>
          )
        })}
      </TableBody>
    </Table>
  )
}

function StepTimeline({ call }: { call: AgentCall }) {
  if (call.stepHistory.length === 0)
    return <Typography variant="caption" color="text.secondary">Step: {call.currentStep}</Typography>

  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell sx={{ pl: 0 }}>Step</TableCell>
          <TableCell>Time</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {call.stepHistory.map((e, i) => (
          <TableRow key={i}>
            <TableCell sx={{ pl: 0, border: 0 }}>
              <Typography variant="caption">{e.step}</Typography>
            </TableCell>
            <TableCell sx={{ border: 0 }}>
              <Typography variant="caption" color="text.secondary">{new Date(e.at).toLocaleTimeString()}</Typography>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
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
              <Box sx={{ mb: 1.5 }}>
                <Typography variant="caption" color="text.secondary" fontWeight={600} display="block" sx={{ mb: 0.5 }}>
                  Saga steps
                </Typography>
                <StepTimeline call={call} />
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary" fontWeight={600} display="block" sx={{ mb: 0.5 }}>
                  Tool calls
                </Typography>
                <ToolCallList agentCallId={call.id} />
              </Box>
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
