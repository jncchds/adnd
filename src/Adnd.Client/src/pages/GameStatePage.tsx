import { useCallback, useState } from 'react';
import { useGame, useGMStatus } from '../api/hooks/useGame';
import { Box, Typography, Tabs, Tab, Chip } from '@mui/material';
import GameStateOverview from '../components/game-state/GameStateOverview';
import GameStateCombat from '../components/game-state/GameStateCombat';
import GameStatePlot from '../components/game-state/GameStatePlot';
import GameStateAgent from '../components/game-state/GameStateAgent';
import GameStateMessages from '../components/game-state/GameStateMessages';
import GameStateLLM from '../components/game-state/GameStateLLM';
import GameStateTriggers from '../components/game-state/GameStateTriggers';

interface GameStatePageProps {
  gameId: string;
}

export default function GameStatePage({ gameId }: GameStatePageProps) {
  const [activeTab, setActiveTab] = useState(0);
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({});
  const { game, isLoading } = useGame(gameId);
  const { status: gmStatus } = useGMStatus(gameId);

  const onToggleSection = useCallback((section: string) => {
    setExpandedSections(prev => ({ ...prev, [section]: !prev[section] }));
  }, []);

  const onOpenAgentDialog = useCallback((call: any) => {
    console.log('Open agent dialog:', call);
  }, []);

  const onTrigger = useCallback((trigger: string) => {
    console.log('Trigger:', trigger);
  }, []);

  if (isLoading) return <Typography>Loading...</Typography>;
  if (!game) return <Typography>No game data available.</Typography>;

  const tabs = [
    { label: 'Overview', icon: '📊' },
    { label: 'Combat', icon: '⚔️' },
    { label: 'Plot', icon: '📖' },
    { label: 'Agents', icon: '🤖' },
    { label: 'Messages', icon: '💬' },
    { label: 'LLM', icon: '🧠' },
    { label: 'Triggers', icon: '🎯' },
  ];

  return (
    <Box sx={{ p: 2 }}>
      <Box sx={{ borderBottom: 1, borderColor: 'divider', mb: 2 }}>
        <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)}>
          {tabs.map((tab, i) => (
            <Tab key={i} label={`${tab.icon} ${tab.label}`} />
          ))}
        </Tabs>
      </Box>

      <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
        <Chip label={`Status: ${game.status}`} color="success" size="small" />
        <Chip label={`GM: ${gmStatus?.status || 'Idle'}`} color={gmStatus?.status === 'Running' ? 'warning' : 'default'} size="small" />
      </Box>

      {activeTab === 0 && <GameStateOverview gameState={{ Game: game, ...game }} expandedSections={expandedSections} onToggleSection={onToggleSection} />}
      {activeTab === 1 && <GameStateCombat gameState={{ ActiveCombats: [], ...game }} />}
      {activeTab === 2 && <GameStatePlot gameState={{ PlotThreads: [], ...game }} />}
      {activeTab === 3 && <GameStateAgent gameState={{ AgentCalls: [], ...game }} onOpenAgentDialog={onOpenAgentDialog} />}
      {activeTab === 4 && <GameStateMessages gameState={{ RecentMessages: [], ...game }} />}
      {activeTab === 5 && <GameStateLLM gameState={{ LLMStats: {}, ...game }} />}
      {activeTab === 6 && <GameStateTriggers gameState={{ Game: game }} onTrigger={onTrigger} loading={false} onOpenReview={() => {}} onOpenAgent={() => {}} />}
    </Box>
  );
}
