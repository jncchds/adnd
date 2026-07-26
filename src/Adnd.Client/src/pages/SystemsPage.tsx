import { useEffect, useState } from 'react'
import { Box, Typography, Paper, Chip, CircularProgress, Alert } from '@mui/material'
import { api } from '../api/client'

export default function SystemsPage() {
  const [systems, setSystems] = useState<{ id: string; name: string }[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true)
    api.systems.list()
      .then(setSystems)
      .catch(e => setError((e as Error).message))
      .finally(() => setLoading(false))
  }, [])

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" fontWeight={700} sx={{ mb: 3 }}>RPG Systems</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {loading && <CircularProgress />}
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        {systems.map(s => (
          <Paper key={s.id} sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 2 }}>
            <Chip label={s.id} size="small" color="primary" variant="outlined" />
            <Typography variant="body1">{s.name}</Typography>
          </Paper>
        ))}
      </Box>
    </Box>
  )
}
