import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../api/client';
import AdminLLMLogsTab from '../components/admin/AdminLLMLogsTab';
import LLMLogDetailDialog from '../components/admin/LLMLogDetailDialog';
import { Box } from '@mui/material';

export default function AdminLLMLogsPage() {
  const { id } = useParams<{ id: string }>();
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
      const logs = await api.getLLMInteractions({
        gameId: id,
        providerType: logFilterProvider || undefined,
        from: logFilterFrom || undefined,
        to: logFilterTo || undefined,
        limit: 200,
      });
      if (logs) setLlmLogs(logs);
    } catch (e) {
      console.error('Failed to fetch LLM logs', e);
    }
    setLogsLoading(false);
  };

  const handleDeleteLog = async (logId: string) => {
    try {
      await api.deleteLLMInteraction(logId);
      setLlmLogs(prev => prev.filter((l: any) => l.id !== logId));
    } catch (e) {
      console.error('Failed to delete log', e);
    }
  };

  const handleOpenDetail = (log: any) => {
    setSelectedLog(log);
    setShowLogDetail(true);
  };

  if (!id) return null;
  return (
    <Box>
      <AdminLLMLogsTab
        logs={llmLogs}
        isLoading={logsLoading}
        onRefresh={handleRefresh}
        onOpenDetail={handleOpenDetail}
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
