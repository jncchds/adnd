import { useState } from 'react';
import { useAuth } from '../api/hooks/useAuth';
import { Box, Typography, Paper, Tabs, Tab, Alert, AlertTitle } from '@mui/material';
import { Lock as LockIcon, BarChart as BarChartIcon, History as HistoryIcon } from '@mui/icons-material';
import PasswordTab from '../components/admin/PasswordTab';
import LLMStatsTab from '../components/admin/LLMStatsTab';
import LLMLogsTab from '../components/admin/LLMLogsTab';

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
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>User Settings</Typography>
      {success && (
        <Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2, alignItems: 'center' }}>{success}</Alert>
      )}
      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {error}
        </Alert>
      )}
      <Paper sx={{ mb: 3 }}>
        <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)} sx={{ borderBottom: 1, borderColor: 'divider', px: 2 }}>
          <Tab icon={<LockIcon fontSize="small" />} label="Password" sx={{ textTransform: 'none', minWidth: 120 }} />
          <Tab icon={<BarChartIcon fontSize="small" />} label="LLM Statistics" sx={{ textTransform: 'none', minWidth: 120 }} />
          <Tab icon={<HistoryIcon fontSize="small" />} label="LLM Logs" sx={{ textTransform: 'none', minWidth: 120 }} />
        </Tabs>
      </Paper>
      {activeTab === 0 && <PasswordTab onError={setError} onSuccess={handleSuccess} onRefreshUser={refreshUser} />}
      {activeTab === 1 && <LLMStatsTab />}
      {activeTab === 2 && <LLMLogsTab onError={setError} onSuccess={handleSuccess} />}
    </Box>
  );
}
