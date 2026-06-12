import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useGameHub } from '../api/hooks/useHub';
import AdminLLMLogsTab from '../components/admin/AdminLLMLogsTab';
import LLMLogDetailDialog from '../components/admin/LLMLogDetailDialog';
import { Box } from '@mui/material';

export default function AdminLLMLogsPage() {
  const { id } = useParams<{ id: string }>();
  const { invoke } = useGameHub();
  const [llmLogs, setLlmLogs] = useState<any[]>([]);
  const [logsLoading, setLogsLoading] = useState(false);
  const [selectedLog, setSelectedLog] = useState<any>(null);
  const [showLogDetail, setShowLogDetail] = useState(false);
  const [logFilterProvider, setLogFilterProvider] = useState('');
  const [logFilterFrom, setLogFilterFrom] = useState('');
  const [logFilterTo, setLogFilterTo] = useState('');

  const handleRefresh = async () => {
    if (!id) return;
    setLogsLoading(true);
    try {
      const logs = await invoke('GetLLMInteractions', id, undefined, logFilterProvider, logFilterFrom || undefined, logFilterTo || undefined, 200);
      if (logs) setLlmLogs(logs);
    } catch (e) {
      console.error('Failed to fetch LLM logs', e);
    }
    setLogsLoading(false);
  };

  const handleDeleteLog = async (logId: string) => {
    try {
      await invoke('DeleteLLMInteraction', logId);
      setLlmLogs(prev => prev.filter(l => l.id !== logId));
    } catch (e) {
      console.error('Failed to delete log', e);
    }
  };

  if (!id) return null;
  return (
    <Box>
      <AdminLLMLogsTab
        logs={llmLogs}
        isLoading={logsLoading}
        onRefresh={handleRefresh}
        onOpenDetail={setSelectedLog}
        onDelete={handleDeleteLog}
        filterProvider={logFilterProvider}
        onFilterProviderChange={setLogFilterProvider}
        filterFrom={logFilterFrom}
        onFilterFromChange={setLogFilterFrom}
        filterTo={logFilterTo}
        onFilterToChange={setLogFilterTo}
      />
      <LLMLogDetailDialog open={showLogDetail} onClose={() => setShowLogDetail(false)} log={selectedLog} />
    </Box>
  );
}
