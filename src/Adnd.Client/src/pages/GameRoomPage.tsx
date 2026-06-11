import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../api/hooks/useAuth';
import { useGame, useSessions, usePlayers, useGMStatus, useSway } from '../api/hooks/useGame';
import { useGameHub } from '../api/hooks/useHub';
import { useGameGameState } from '../api/hooks/useGameState';
import { useToolCalls } from '../api/hooks/useAgent';
import { useMessagesInfiniteScroll, UnifiedMessage } from '../api/hooks/useMessages';
import { api } from '../api/client';
import { WhisperType, AgentType, AgentAction } from '../types';
import CombatTab from './CombatTab';
import CharacterCreateWizard from './CharacterCreateWizard';
import ToolCallBanner from '../components/ToolCallBanner';
import PlayerRollDialog from '../components/PlayerRollDialog';
import ChatPanel from '../components/chat/ChatPanel';
import { Box, Typography, Paper, Tabs, Tab, Grid, IconButton, Collapse, Chip, Divider, Button, TextField, InputAdornment, MenuItem, Select, FormControl, InputLabel } from '@mui/material';
import { Send as SendIcon, SportsEsports as DiceIcon, Replay as ReplayIcon, ExitToApp as LeaveIcon, People as PeopleIcon } from '@mui/icons-material';

type MessageInputType = 'inGame' | 'ooc';
type MessageTarget = 'all' | 'gm' | 'player';

export default function GameRoomPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { game, isLoading: isLoadingGame } = useGame(id);
  const { sessions } = useSessions(id);
  const { players } = usePlayers(id);
  const { status: gmStatus } = useGMStatus(id);
  const { sway } = useSway(id);
  const gameState = useGameGameState(id);
  const { calls } = useToolCalls(id);
  const { messages, isLoading, hasMore, loadMore, loadOldest } = useMessagesInfiniteScroll(id, undefined);
  const hub = useGameHub();
  const [activeTab, setActiveTab] = useState(0);
  const [messageInput, setMessageInput] = useState('');
  const [messageType, setMessageType] = useState<MessageInputType>('inGame');
  const [messageTarget, setMessageTarget] = useState<MessageTarget>('all');
  const [showSettings, setShowSettings] = useState(false);
  const [showCharacterWizard, setShowCharacterWizard] = useState(false);
  const [selectedSession, setSelectedSession] = useState<string | null>(null);
  const chatRef = useRef<HTMLDivElement>(null);

  const sendMessage = useCallback(async () => {
    if (!messageInput.trim() || !id) return;
    // TODO: implement send via hub
    setMessageInput('');
  }, [messageInput, id]);

  const handleRollDice = useCallback(async () => {
    // TODO: implement dice roll
  }, []);

  const handleLeaveGame = useCallback(async () => {
    if (!id) return;
    await api.leaveGame(id);
    navigate('/');
  }, [id, navigate]);

  if (isLoadingGame) return <Typography>Loading...</Typography>;
  if (!game) return <Typography>No game data available.</Typography>;

  const tabs = [
    { label: 'Chat', icon: '💬' },
    { label: 'Combat', icon: '⚔️' },
    { label: 'Characters', icon: '👤' },
    { label: 'Settings', icon: '⚙️' },
  ];

  return (
    <Box sx={{ display: 'flex', height: '100vh' }}>
      <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
          <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)}>
            {tabs.map((tab, i) => (
              <Tab key={i} label={`${tab.icon} ${tab.label}`} />
            ))}
          </Tabs>
        </Box>

        <Box sx={{ display: 'flex', flex: 1, overflow: 'hidden' }}>
          <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', p: 2 }}>
            {activeTab === 0 && (
              <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                <Box sx={{ flex: 1, overflow: 'auto', mb: 2 }} ref={chatRef}>
                  <ChatPanel
                    messages={messages}
                    isLoadingMore={isLoading}
                    hasMore={hasMore}
                    loadMoreOldest={loadOldest}
                  />
                </Box>

                <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                  <FormControl size="small" sx={{ minWidth: 120 }}>
                    <InputLabel>Type</InputLabel>
                    <Select
                      value={messageType}
                      label="Type"
                      onChange={(e) => setMessageType(e.target.value as MessageInputType)}
                    >
                      <MenuItem value="inGame">In-Game</MenuItem>
                      <MenuItem value="ooc">OOC</MenuItem>
                    </Select>
                  </FormControl>

                  <TextField
                    fullWidth
                    placeholder={`Send ${messageType} message...`}
                    value={messageInput}
                    onChange={(e) => setMessageInput(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && sendMessage()}
                    InputProps={{
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton onClick={sendMessage} edge="end">
                            <SendIcon />
                          </IconButton>
                          <IconButton onClick={handleRollDice} edge="end">
                            <DiceIcon />
                          </IconButton>
                        </InputAdornment>
                      ),
                    }}
                  />
                </Box>
              </Box>
            )}

            {activeTab === 1 && id && <CombatTab gameId={id} />}
            {activeTab === 2 && (
              <Box sx={{ p: 2 }}>
                <Button variant="contained" onClick={() => setShowCharacterWizard(true)}>
                  Create Character
                </Button>
              </Box>
            )}
            {activeTab === 3 && (
              <Box sx={{ p: 2 }}>
                <Button variant="outlined" onClick={() => setShowSettings(!showSettings)}>
                  {showSettings ? 'Hide' : 'Show'} Settings
                </Button>
                {showSettings && (
                  <Box sx={{ mt: 2 }}>
                    <Typography>Game Settings</Typography>
                    <Typography>Game ID: {id}</Typography>
                    <Typography>GM Status: {gmStatus?.status || 'Idle'}</Typography>
                    <Typography>Players: {players?.length || 0}</Typography>
                  </Box>
                )}
              </Box>
            )}
          </Box>
        </Box>
      </Box>

      <ToolCallBanner
        pendingCalls={calls?.pendingCalls || []}
        onConfirm={async (callId, approved) => {
          // TODO: implement
        }}
        onRoll={async (callId) => {
          // TODO: implement
        }}
        onDecline={async (callId) => {
          // TODO: implement
        }}
        onDismiss={(callId) => {
          // TODO: implement
        }}
        isCreator={user?.role === 'Creator'}
      />

      {showCharacterWizard && id && (
        <CharacterCreateWizard gameId={id} onClose={() => setShowCharacterWizard(false)} />
      )}
    </Box>
  );
}
