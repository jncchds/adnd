import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useNPCs } from '../api/hooks/useNPCs';
import AdminNPCDialog from '../components/admin/AdminNPCDialog';
import AdminNPCsTab from '../components/admin/AdminNPCsTab';
import { Box, Alert, AlertTitle } from '@mui/material';

export default function AdminNPCsPage() {
  const { id } = useParams<{ id: string }>();
  const { npcs, isLoading: npcsLoading, createNPC, updateNPC, deleteNPC } = useNPCs(id);
  const [npcDialogOpen, setNpcDialogOpen] = useState(false);
  const [errorState, setErrorState] = useState<string | null>(null);

  if (!id) return null;

  return (
    <Box>
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}
      <AdminNPCsTab
        npcs={npcs}
        npcsLoading={npcsLoading}
        onOpenDialog={() => setNpcDialogOpen(true)}
        onDelete={deleteNPC}
        onUpdate={updateNPC}
      />
      <AdminNPCDialog open={npcDialogOpen} onClose={() => setNpcDialogOpen(false)} onCreate={async (name, desc) => {
        if (!id) return;
        await createNPC!(name, desc);
        setNpcDialogOpen(false);
      }} />
    </Box>
  );
}
