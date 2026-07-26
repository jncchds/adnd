import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { Box, Typography, Button, CircularProgress, Alert, Paper, List, ListItem, ListItemText } from '@mui/material'
import { api } from '../api/client'
import type { ConsistencyReport, PlotContinuation } from '../types'

export default function AdminConsistencyPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const [report, setReport] = useState<ConsistencyReport | null>(null)
  const [continuation, setContinuation] = useState<PlotContinuation | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const check = async () => {
    if (!gameId) return
    setLoading(true)
    setError(null)
    try {
      const [r, c] = await Promise.all([api.plots.consistency(gameId), api.plots.continuation(gameId)])
      setReport(r)
      setContinuation(c)
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  return (
    <Box sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" fontWeight={700}>Story Consistency</Typography>
        <Button variant="contained" onClick={check} disabled={loading}>
          {loading ? <CircularProgress size={18} /> : 'Run Consistency Check'}
        </Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {report && (
        <Paper sx={{ p: 2, mb: 2 }}>
          <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 1 }}>
            Consistency: {report.isConsistent ? '✓ Consistent' : '⚠ Issues Found'}
          </Typography>
          {report.issues.length > 0 && (
            <>
              <Typography variant="body2" color="error" sx={{ mb: 0.5 }}>Issues:</Typography>
              <List dense>
                {report.issues.map((issue, i) => (
                  <ListItem key={i} sx={{ py: 0 }}>
                    <ListItemText primary={issue} primaryTypographyProps={{ variant: 'body2' }} />
                  </ListItem>
                ))}
              </List>
            </>
          )}
          {report.suggestions.length > 0 && (
            <>
              <Typography variant="body2" color="primary" sx={{ mb: 0.5, mt: 1 }}>Suggestions:</Typography>
              <List dense>
                {report.suggestions.map((s, i) => (
                  <ListItem key={i} sx={{ py: 0 }}>
                    <ListItemText primary={s} primaryTypographyProps={{ variant: 'body2' }} />
                  </ListItem>
                ))}
              </List>
            </>
          )}
        </Paper>
      )}

      {continuation && (
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 1 }}>Story Continuation Suggestions</Typography>
          {continuation.suggestions.map((s, i) => (
            <Typography key={i} variant="body2" sx={{ mb: 0.5 }}>• {s}</Typography>
          ))}
          {continuation.nextMilestones.length > 0 && (
            <>
              <Typography variant="body2" color="primary" sx={{ mt: 1, mb: 0.5 }}>Next Milestones:</Typography>
              {continuation.nextMilestones.map((m, i) => (
                <Typography key={i} variant="body2" sx={{ mb: 0.5 }}>• {m}</Typography>
              ))}
            </>
          )}
        </Paper>
      )}
    </Box>
  )
}
