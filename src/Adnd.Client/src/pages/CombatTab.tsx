import { useState } from 'react';
import { useGameHub } from '../api/hooks/useHub';
import { useCombats, useCombat } from '../api/hooks/useCombat';
import { Box, Typography, Tabs, Tab, Button } from '@mui/material';
import CombatLogPanel from '../components/combat/CombatLogPanel';

interface CombatTabProps {
  gameId: string;
}

export default function CombatTab({ gameId }: CombatTabProps) {
  const [activeTab, setActiveTab] = useState(0);
  const [showCombatLog, setShowCombatLog] = useState(false);
  const [_selectedParticipant, _setSelectedParticipant] = useState<string | null>(null);
  const [_showSheet, _setShowSheet] = useState(false);

  const { refetch: refetchCombats } = useCombats(gameId);
  const { combat } = useCombat(undefined, gameId);
  const hub = useGameHub();

  const handleStartCombat = async () => {
    if (!gameId) return;
    await hub.invoke('StartCombat', gameId);
    refetchCombats();
  };

  const handleEndCombat = async () => {
    if (!gameId) return;
    await hub.invoke('EndCombat', gameId);
    refetchCombats();
  };

  const tabs = [
    { label: 'Initiative', icon: '🎲' },
    { label: 'Combat Log', icon: '📋' },
    { label: 'Conditions', icon: '🔴' },
    { label: 'Action Economy', icon: '🎯' },
  ];

  return (
    <Box sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
        <Button variant="contained" color="error" onClick={handleStartCombat}>
          Start Combat
        </Button>
        <Button variant="outlined" color="error" onClick={handleEndCombat}>
          End Combat
        </Button>
        <Button variant="outlined" onClick={() => setShowCombatLog(!showCombatLog)}>
          {showCombatLog ? 'Hide' : 'Show'} Log
        </Button>
      </Box>

      {showCombatLog && combat && (
        <Box sx={{ mb: 2 }}>
          <CombatLogPanel events={combat.events || []} showCombatLog={showCombatLog} />
        </Box>
      )}

      <Box sx={{ borderBottom: 1, borderColor: 'divider', mb: 2 }}>
        <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)}>
          {tabs.map((tab, i) => (
            <Tab key={i} label={`${tab.icon} ${tab.label}`} />
          ))}
        </Tabs>
      </Box>

      {activeTab === 0 && (
        <Box>
          <Typography>Initiative Order</Typography>
          {/* TODO: Render initiative order */}
        </Box>
      )}
      {activeTab === 1 && (
        <Box>
          <Typography>Combat Log</Typography>
          {/* TODO: Render combat log */}
        </Box>
      )}
      {activeTab === 2 && (
        <Box>
          <Typography>Conditions</Typography>
          {/* TODO: Render conditions */}
        </Box>
      )}
      {activeTab === 3 && (
        <Box>
          <Typography>Action Economy</Typography>
          {/* TODO: Render action economy */}
        </Box>
      )}
    </Box>
  );
}
