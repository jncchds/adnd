import { useState } from 'react'
import {
  Box, Typography, Paper, TextField, Button, Alert, CircularProgress, Divider,
} from '@mui/material'
import { useAuth } from '../context/AuthContext'
import { api } from '../api/client'

export default function UserSettingsPage() {
  const { user, refresh } = useAuth()
  const [displayName, setDisplayName] = useState(user?.displayName ?? '')
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const saveDisplayName = async () => {
    setSaving(true)
    setError(null)
    setSuccess(null)
    try {
      await api.auth.updateDisplayName(displayName)
      await refresh()
      setSuccess('Display name updated.')
    } catch (e) { setError((e as Error).message) }
    finally { setSaving(false) }
  }

  const changePassword = async () => {
    if (!currentPassword || !newPassword) { setError('Both fields required'); return }
    setSaving(true)
    setError(null)
    setSuccess(null)
    try {
      await api.auth.changePassword(currentPassword, newPassword)
      setCurrentPassword('')
      setNewPassword('')
      setSuccess('Password changed.')
    } catch (e) { setError((e as Error).message) }
    finally { setSaving(false) }
  }

  return (
    <Box sx={{ p: 3, maxWidth: 500 }}>
      <Typography variant="h5" fontWeight={700} sx={{ mb: 3 }}>User Settings</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }}>{success}</Alert>}

      <Paper sx={{ p: 2, mb: 2 }}>
        <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 2 }}>Profile</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>Email: {user?.email}</Typography>
        <TextField label="Display Name" value={displayName} onChange={e => setDisplayName(e.target.value)} fullWidth sx={{ mb: 2 }} />
        <Button variant="contained" onClick={saveDisplayName} disabled={saving}>
          {saving ? <CircularProgress size={18} /> : 'Save Name'}
        </Button>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 2 }}>Change Password</Typography>
        <Divider sx={{ mb: 2 }} />
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField label="Current Password" type="password" value={currentPassword}
            onChange={e => setCurrentPassword(e.target.value)} fullWidth />
          <TextField label="New Password" type="password" value={newPassword}
            onChange={e => setNewPassword(e.target.value)} fullWidth />
          <Button variant="outlined" onClick={changePassword} disabled={saving}>Change Password</Button>
        </Box>
      </Paper>
    </Box>
  )
}
