import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useAuth } from '../api/hooks/useAuth';
import { useGameHub } from '../api/hooks/useHub';
import { useCombats, useCombat } from '../api/hooks/useCombat';
import { Box, Typography, Paper, Tabs, Tab, Grid, IconButton, Collapse, Chip, Divider, Button, TextField, InputAdornment, MenuItem, Select, FormControl, InputLabel } from '@mui/material';
import { Send as SendIcon, SportsEsports as DiceIcon, Replay as ReplayIcon, ExitToApp as LeaveIcon, People as PeopleIcon } from '@mui/icons-material';
import CombatLogPanel from '../components/combat/CombatLogPanel';
import DeathSaveTracker from '../components/combat/DeathSaveTracker';
import ActionEconomyTracker from '../components/combat/ActionEconomyTracker';
import ConditionManager from '../components/combat/ConditionManager';
import CharacterSheetPopup from '../components/combat/CharacterSheetPopup';

interface CombatTabProps {
  gameId: string;
}

export default function CombatTab({ gameId }: CombatTabProps) {
  const [activeTab, setActiveTab] = useState(0);
  const [showCombatLog, setShowCombatLog] = useState(false);
  const [selectedParticipant, setSelectedParticipant] = useState<string | null>(null);
  const [showSheet, setShowSheet] = useState(false);

  const { combats, refetch: refetchCombats } = useCombats(gameId);
  const { combat, refetch: refetchCombat } = useCombat(undefined, gameId);
  const { user } = useAuth();
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
