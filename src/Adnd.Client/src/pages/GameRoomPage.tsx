import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../api/authHook';
import { useGame, useSessions, usePlayers, useCharacters, useGMStatus, useSway } from '../api/gameHooks';
import { useGameHub } from '../api/hubHook';
import { api } from '../api/client';
import { WhisperType, AgentType, AgentAction, AgentCallStatus, MessageType } from '../types';
import { WhisperResponse } from '../api/hubHook';
import CombatTab from './CombatTab';
import {
  Container, Box, Typography, Paper, TextField, Button, Tabs, Tab,
  List, ListItem, ListItemText, ListItemAvatar, Avatar, Chip,
  Dialog, DialogTitle, DialogContent, DialogActions,
  IconButton, Divider, Alert, AlertTitle, Collapse,
  InputAdornment, MenuItem, Select, FormControl,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow
} from '@mui/material';
import { Send as SendIcon, DirectionsRun as ActionIcon, SportsEsports as DiceIcon,
  People as PeopleIcon, Settings as SettingsIcon,
  Replay as ReplayIcon, ExitToApp as LeaveIcon,
  Chat as ChatBubbleIcon, Mic as MicIcon, Article as SheetIcon,
  DirectionsRun as CombatIcon } from '@mui/icons-material';

export default function GameRoomPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { game, isLoading } = useGame(id);
  const { sessions, refetch: refetchSessions } = useSessions(id);
  const { players, refetch: refetchPlayers } = usePlayers(id);
  const { characters } = useCharacters(id);
  const { status: gmStatus, refetch: refetchGMStatus, pause: pauseGM, resume: resumeGM } = useGMStatus(id);
  const { sway, lastSway, isLoading: swayLoading } = useSway(id);
  const { isConnected, connect, on, invoke, disconnect } = useGameHub();

  const [activeTab, setActiveTab] = useState(0);
  const [message, setMessage] = useState('');           // In-game public chat
  const [inGameMessages, setInGameMessages] = useState<any[]>([]);
  const [oocMessages, setOOCMessages] = useState<any[]>([]);
  const [whispers, setWhispers] = useState<any[]>([]);
  const [oocWhispers, setOOCWhispers] = useState<any[]>([]);
  const [agentCalls, setAgentCalls] = useState<any[]>([]);
  const [selectedSession, setSelectedSession] = useState<string | null>(null);
  const [openDiceDialog, setOpenDiceDialog] = useState(false);
  const [diceFormula, setDiceFormula] = useState('1d20');
  const [diceResults, setDiceResults] = useState<any[]>([]);
  const [showDiceHistory, setShowDiceHistory] = useState(false);
  const [createSessionOpen, setCreateSessionOpen] = useState(false);
  const [sessionTitle, setSessionTitle] = useState('');
  const [errorState, setErrorState] = useState<string | null>(null);
  const [successState, setSuccessState] = useState<string | null>(null);
  const [activeCombats, setActiveCombats] = useState<any[]>([]);
  const [whisperContent, setWhisperContent] = useState('');
  const [oocMessage, setOocMessage] = useState('');
  const [oocWhisperContent, setOocWhisperContent] = useState('');
  const [swayInput, setSwayInput] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Connect to SignalR hub
  useEffect(() => {
    if (id && user) {
      const token = localStorage.getItem('token');
      connect(token || undefined);
    }
    return () => { disconnect(); };
  }, [id, user, connect, disconnect]);

  // Set first active session as default
  useEffect(() => {
    if (sessions.length > 0 && !selectedSession) {
      const activeSession = sessions.find(s => !s.endedAt);
      setSelectedSession(activeSession?.id || sessions[0].id);
    }
  }, [sessions, selectedSession]);

  // Listen for incoming messages
  useEffect(() => {
    if (!isConnected) return;

    on('NewMessage', (msg: any) => {
      // In-game public message - part of narrative
      setInGameMessages(prev => [...prev, msg]);
    });

    on('NewOOCMessage', (msg: any) => {
      // OOC public message - separate channel
      setOOCMessages(prev => [...prev, msg]);
    });

    on('NewWhisper', (whisper: any) => {
      // In-game whisper (to GM or from GM)
      setWhispers(prev => [...prev, whisper]);
    });

    on('NewOOCWhisper', (whisper: any) => {
      // OOC whisper (player↔GM clarification)
      setOOCWhispers(prev => [...prev, whisper]);
    });

    on('DiceRollResult', (result: any) => {
      setDiceResults(prev => [result, ...prev]);
      setShowDiceHistory(true);
    });

    on('PlayerJoined', () => {
      refetchPlayers();
    });

    on('PlayerLeft', () => {
      refetchPlayers();
    });

    on('SkillCheckResult', (result: any) => {
      setInGameMessages(prev => [...prev, {
        id: Date.now(),
        type: 'SkillCheck',
        content: `${result.Skill}: d20(${result.DiceRoll})+${result.Modifier}=${result.Total} vs DC ${result.DC} -> ${result.Success ? 'SUCCESS' : 'FAILURE'}`,
        timestamp: result.RolledAt,
        isSystem: true
      }]);
    });

    on('AttackResult', (result: any) => {
      setInGameMessages(prev => [...prev, {
        id: Date.now(),
        type: 'Attack',
        content: `${result.Weapon} vs ${result.Target}: ${result.Hit ? `HIT! ${result.DamageTotal} damage` : 'MISS'}`,
        timestamp: result.RolledAt,
        isSystem: true
      }]);
    });

    on('Error', (err: any) => {
      setErrorState(err.message);
    });

    on('NewWhisper', (whisper: any) => {
      setWhispers(prev => [...prev, whisper]);
    });

    on('AgentCallStarted', (call: any) => {
      setAgentCalls(prev => [...prev, { ...call, status: AgentCallStatus.Running }]);
    });

    on('AgentCallCompleted', (call: any) => {
      setAgentCalls(prev => {
        const existing = prev.find((c: any) => c.id === call.id);
        if (existing) {
          return prev.map((c: any) => c.id === call.id ? { ...c, ...call } : c);
        }
        return [...prev, call];
      });
    });

    return () => {
      // Cleanup
    };
  }, [isConnected, on, refetchPlayers]);

  // Scroll to bottom on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [inGameMessages, oocMessages]);

  // Load active combats
  useEffect(() => {
    if (id && isConnected) {
      api.getActiveCombats(id).then((combats: unknown) => {
        const c = combats as any[];
        if (c && c.length > 0) setActiveCombats(c);
      }).catch(() => {});
    }
  }, [id, isConnected]);

  // In-game public message (narrative)
  const handleSendMessage = async () => {
    if (!message.trim() || !selectedSession) return;

    try {
      await invoke('SendMessage', selectedSession, message);
      setMessage('');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  // In-game whisper to GM (adds to GM knowledge) - GM sends via Whispers tab
  // Players whisper to GM via the Whisper button in Chat tab

  // OOC public message
  const handleSendOOCMessage = async () => {
    if (!oocMessage.trim() || !selectedSession) return;

    try {
      await invoke('SendOOCMessage', selectedSession, oocMessage);
      setOocMessage('');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  // OOC whisper from player to GM
  const handleSendOOCWhisper = async () => {
    if (!oocWhisperContent.trim() || !selectedSession) return;

    try {
      await invoke('SendOOCWhisper', selectedSession, oocWhisperContent);
      setOocWhisperContent('');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  // GM sends OOC whisper to player
  const handleSendOOCWhisperToPlayer = async (targetPlayerId: string) => {
    if (!oocWhisperContent.trim()) return;

    try {
      await invoke('SendOOCWhisperToPlayer', targetPlayerId, oocWhisperContent);
      setOocWhisperContent('');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  // GM sends in-game whisper to player (e.g., divination result)
  const handleSendInGameWhisperToPlayer = async (targetPlayerId: string) => {
    if (!whisperContent.trim()) return;

    try {
      await invoke('SendInGameWhisperToPlayer', targetPlayerId, whisperContent);
      setWhisperContent('');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleRefreshAgentCalls = async () => {
    if (!id) return;
    try {
      const calls = await invoke('GetAgentCallHistory', id);
      if (calls) setAgentCalls(calls);
    } catch (e) {
      // ignore
    }
  };

  const handleDiceRoll = async () => {
    if (!selectedSession) return;

    try {
      const result = await invoke('RollDice', selectedSession, diceFormula, user?.id);
      setDiceResults(prev => [result, ...prev]);
      setShowDiceHistory(true);
      setOpenDiceDialog(false);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleSkillCheck = async (skill: string, dc: number) => {
    if (!selectedSession) return;

    try {
      await invoke('SkillCheck', selectedSession, skill, user?.id, dc);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleAttack = async (weapon: string, target: string) => {
    if (!selectedSession) return;

    try {
      await invoke('Attack', selectedSession, weapon, target, user?.id);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleCreateSession = async () => {
    if (!id || !sessionTitle.trim()) return;
    try {
      await api.createSession(id, sessionTitle);
      setCreateSessionOpen(false);
      setSessionTitle('');
      refetchSessions();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleLeave = async () => {
    if (!id) return;
    try {
      disconnect();
      setSuccessState('Left game successfully.');
      setTimeout(() => navigate('/dashboard'), 500);
    } catch (e: any) {
      navigate('/dashboard');
    }
  };

  const handleSway = async () => {
    if (!swayInput.trim() || !id) return;
    try {
      const result = await sway(swayInput);
      if (result) {
        setSwayInput('');
        refetchGMStatus();
      }
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handlePauseGM = async () => {
    if (!id) return;
    try {
      await pauseGM();
      refetchGMStatus();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleResumeGM = async () => {
    if (!id) return;
    try {
      await resumeGM();
      refetchGMStatus();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  if (isLoading) {
    return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading game...</Typography></Box>;
  }

  if (!game) {
    return <Box sx={{ textAlign: 'center', mt: 8 }}>
      <Typography variant="h5" color="error">Game not found</Typography>
      <Button onClick={() => navigate('/dashboard')} sx={{ mt: 2 }}>Back to Dashboard</Button>
    </Box>;
  }

  const tabs = [
    { label: 'Chat', icon: <SendIcon />, count: inGameMessages.length },
    { label: 'OOC', icon: <ChatBubbleIcon />, count: oocMessages.length },
    { label: 'Combat', icon: <CombatIcon />, count: activeCombats.length > 0 ? activeCombats.length : undefined },
    { label: 'Whispers', icon: <MicIcon />, count: whispers.length + oocWhispers.length },
    { label: 'Players', icon: <PeopleIcon />, count: players.length },
    { label: 'Characters', icon: <SheetIcon />, count: characters.length },
    { label: 'Actions', icon: <ActionIcon /> },
    { label: 'Agent Calls', icon: <MicIcon /> },
    { label: 'Settings', icon: <SettingsIcon /> },
  ];

  return (
    <Container maxWidth="lg" sx={{ mt: 2, mb: 2 }}>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Box>
          <Typography variant="h5">{game.name}</Typography>
          <Box sx={{ display: 'flex', gap: 1, mt: 0.5, flexWrap: 'wrap' }}>
            <Chip label={game.systemId} size="small" />
            <Chip label={game.status} size="small" color={game.status === 'Active' ? 'success' : 'default'} />
            {game.llmPresetName && <Chip label={game.llmPresetName} size="small" variant="outlined" />}
            {gmStatus && (
              <Chip
                label={`AI-GM: ${gmStatus.status}`}
                size="small"
                color={gmStatus.status === 'running' ? 'success' : gmStatus.status === 'paused' ? 'warning' : 'default'}
              />
            )}
            {isConnected && <Chip label="Connected" size="small" color="success" icon={<ReplayIcon fontSize="small" />}></Chip>}
            {!isConnected && <Chip label="Disconnected" size="small" color="error" />}</Box>
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          {game.status === 'Active' && gmStatus?.status === 'running' && (
            <Button size="small" variant="outlined" onClick={handlePauseGM}>Pause GM</Button>
          )}
          {game.status === 'Active' && gmStatus?.status === 'paused' && (
            <Button size="small" variant="outlined" onClick={handleResumeGM}>Resume GM</Button>
          )}
          <Button variant="outlined" color="error" startIcon={<LeaveIcon />} onClick={handleLeave}>
            Leave
          </Button>
        </Box>
      </Box>

      {/* Sway Input (Creator only) */}
      {game.status === 'Active' && gmStatus?.status === 'running' && (
        <Paper sx={{ p: 2, mb: 2, bgcolor: 'warning.lighter' }}>
          <Typography variant="subtitle2" gutterBottom sx={{ color: 'warning.dark' }}>
            🎬 Sway the Story (Creator Only)
          </Typography>
          <Box sx={{ display: 'flex', gap: 1 }}>
            <TextField
              fullWidth
              size="small"
              placeholder="Describe a narrative direction to nudge the AI-GM..."
              value={swayInput}
              onChange={e => setSwayInput(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && handleSway()}
            />
            <Button
              variant="contained"
              onClick={handleSway}
              disabled={!swayInput.trim() || swayLoading}
              sx={{ minWidth: 100 }}
            >
              Sway
            </Button>
          </Box>
          {lastSway && (
            <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
              Last sway: {new Date(lastSway.createdAt).toLocaleString()} (call: {lastSway.callId})
            </Typography>
          )}
        </Paper>
      )}

      {/* Error / Success */}
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}
      {successState && (
        <Alert severity="success" onClose={() => setSuccessState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Success</AlertTitle>
          {successState}
        </Alert>
      )}

      {/* Tabs */}
      <Paper sx={{ mb: 2 }}>
        <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)}>
          {tabs.map((tab, i) => (
            <Tab key={i} label={`${tab.label}${tab.count !== undefined ? ` (${tab.count})` : ''}`} icon={tab.icon} iconPosition="start" />
          ))}
        </Tabs>
      </Paper>

      {/* Tab Content */}
      <Box sx={{ display: 'flex', gap: 2 }}>
        {/* Main Content */}
        <Box sx={{ flex: 1 }}>
          {activeTab === 0 && (
            <ChatTab
              messages={inGameMessages}
              message={message}
              setMessage={setMessage}
              onSend={handleSendMessage}
              onDiceRoll={() => setOpenDiceDialog(true)}
              onSkillCheck={handleSkillCheck}
              diceResults={diceResults}
              showDiceHistory={showDiceHistory}
              setShowDiceHistory={setShowDiceHistory}
              messagesEndRef={messagesEndRef}
              selectedSession={selectedSession}
              sessions={sessions}
              isCreator={players.some((p: any) => p.role === 'Creator')}
              whisperContent={whisperContent}
              setWhisperContent={setWhisperContent}
              players={players}
              onInGameWhisperToPlayer={handleSendInGameWhisperToPlayer}
            />
          )}

          {activeTab === 1 && (
            <OOCTab
              messages={oocMessages}
              oocMessage={oocMessage}
              setOocMessage={setOocMessage}
              onSendOOC={handleSendOOCMessage}
              oocWhisperContent={oocWhisperContent}
              setOocWhisperContent={setOocWhisperContent}
              onSendOOCWhisper={handleSendOOCWhisper}
              oocWhispers={oocWhispers}
              messagesEndRef={messagesEndRef}
              selectedSession={selectedSession}
              sessions={sessions}
              isCreator={players.some((p: any) => p.role === 'Creator')}
              players={players}
              onOOCWhisperToPlayer={handleSendOOCWhisperToPlayer}
            />
          )}

          {activeTab === 3 && (
            <CombatTab gameId={id || ''} />
          )}

          {activeTab === 4 && (
            <WhispersTab
              whispers={[...whispers, ...oocWhispers]}
              whisperContent={whisperContent}
              setWhisperContent={setWhisperContent}
              players={players}
              messagesEndRef={messagesEndRef}
              isCreator={players.some((p: any) => p.role === 'Creator')}
              onInGameWhisperToPlayer={handleSendInGameWhisperToPlayer}
              onOOCWhisperToPlayer={handleSendOOCWhisperToPlayer}
            />
          )}

          {activeTab === 5 && (
            <PlayersTab players={players} />
          )}

          {activeTab === 6 && (
            <CharactersTab characters={characters} />
          )}

          {activeTab === 7 && (
            <ActionsTab onSkillCheck={handleSkillCheck} onAttack={handleAttack} onDiceRoll={() => setOpenDiceDialog(true)} />
          )}

          {activeTab === 8 && (
            <AgentCallsTab
              calls={agentCalls}
              onRefresh={handleRefreshAgentCalls}
            />
          )}

          {activeTab === 9 && (
            <SettingsTab game={game} sessions={sessions} onNewSession={() => setCreateSessionOpen(true)} />
          )}
        </Box>
      </Box>

      {/* Dice Dialog */}
      <Dialog open={openDiceDialog} onClose={() => setOpenDiceDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Dice Roller</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <TextField
            fullWidth
            label="Dice Formula"
            value={diceFormula}
            onChange={e => setDiceFormula(e.target.value)}
            placeholder="e.g., 1d20, 2d6+3, 4d6kh3"
            sx={{ mb: 2 }}
          />
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {['1d4', '1d6', '1d8', '1d10', '1d12', '1d20', '1d100', '2d6', '2d10', '4d6kh3'].map(d => (
              <Chip key={d} label={d} clickable onClick={() => setDiceFormula(d)} size="small" />
            ))}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenDiceDialog(false)}>Cancel</Button>
          <Button onClick={handleDiceRoll} variant="contained" startIcon={<DiceIcon />}>
            Roll {diceFormula}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Create Session Dialog */}
      <Dialog open={createSessionOpen} onClose={() => setCreateSessionOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>New Session</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <TextField
            fullWidth
            label="Session Title"
            value={sessionTitle}
            onChange={e => setSessionTitle(e.target.value)}
            autoFocus
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateSessionOpen(false)}>Cancel</Button>
          <Button onClick={handleCreateSession} variant="contained" disabled={!sessionTitle.trim()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}

// ==================== Sub-Components ====================

function ChatTab({ messages, message, setMessage, onSend, onDiceRoll, onSkillCheck, diceResults, showDiceHistory, setShowDiceHistory, messagesEndRef, selectedSession, sessions, isCreator, whisperContent, players, onInGameWhisperToPlayer }: any) {
  const filteredMessages = selectedSession
    ? messages.filter((m: any) => m.sessionId === selectedSession)
    : messages;
  const activeSession = sessions?.find((s: any) => !s.endedAt);
  const [showWhisperInput, setShowWhisperInput] = useState(false);
  const [whisperTarget, setWhisperTarget] = useState('');

  const isWhisper = (msg: any) => msg.type === MessageType.InGameWhisper;

  return (
    <Paper sx={{ height: '70vh', display: 'flex', flexDirection: 'column' }}>
      {/* Toolbar */}
      <Box sx={{ p: 1, display: 'flex', gap: 1, borderBottom: 1, borderColor: 'divider', flexWrap: 'wrap' }}>
        <Chip label="🎮 In-Game" size="small" color="primary" variant="outlined" />
        <Button size="small" onClick={() => setShowDiceHistory(!showDiceHistory)}>
          🎲 Dice History ({diceResults.length})
        </Button>
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <Select value="15" label="DC" onChange={e => {
            const dc = e.target.value;
            onSkillCheck('Perception', parseInt(dc));
          }}>
            <MenuItem value="10">DC 10</MenuItem>
            <MenuItem value="15">DC 15</MenuItem>
            <MenuItem value="20">DC 20</MenuItem>
            <MenuItem value="25">DC 25</MenuItem>
            <MenuItem value="30">DC 30</MenuItem>
          </Select>
        </FormControl>
        <Button size="small" onClick={() => onSkillCheck('Perception', 15)}>Quick Perception</Button>
        <Button size="small" onClick={onDiceRoll}>🎲 Roll</Button>
        <Button size="small" variant="outlined" onClick={() => setShowWhisperInput(!showWhisperInput)}>
          🤫 Whisper to GM
        </Button>
        {activeSession && (
          <Chip label={`Session: ${activeSession.title}`} size="small" color="primary" variant="outlined" />
        )}
      </Box>

      {/* Whisper input (GM only) */}
      {showWhisperInput && (
        <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography variant="caption" color="text.secondary">Whisper to player:</Typography>
          {players.filter((p: any) => p.status === 'Active').map((p: any) => (
            <Chip
              key={p.id}
              label={p.characterName}
              size="small"
              clickable
              onClick={() => { setWhisperTarget(p.id); setShowWhisperInput(false); }}
              sx={{ m: 0.25 }}
            />
          ))}
        </Box>
      )}

      {/* Dice History */}
      <Collapse in={showDiceHistory}>
        <Box sx={{ p: 1, maxHeight: 150, overflow: 'auto', bgcolor: 'background.default', borderBottom: 1, borderColor: 'divider' }}>
          <Typography variant="caption" color="text.secondary">Recent Rolls:</Typography>
          {diceResults.map((r: any, i: number) => (
            <Typography key={i} variant="caption" sx={{ display: 'block' }}>
              {r.Formula}: {r.Total} [{r.Rolls?.join(',')}] 
            </Typography>
          ))}
        </Box>
      </Collapse>

      {/* Messages */}
      <Box sx={{ flex: 1, overflow: 'auto', p: 2 }}>
        {filteredMessages.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 8 }}>
            No messages yet. Start the conversation!
          </Typography>
        ) : (
          filteredMessages.map((msg: any, i: number) => (
            <Box key={i} sx={{ mb: 1 }}>
              {msg.type === MessageType.Dice && (
                <Chip label={`🎲 ${msg.content}`} size="small" sx={{ mb: 0.5 }} color="primary" variant="outlined" />
              )}
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
                <Avatar sx={{ width: 24, height: 24, fontSize: 12, bgcolor: isWhisper(msg) ? 'warning.main' : 'primary.main' }}>
                  {isWhisper(msg) ? '🤫' : (msg.playerId ? 'P' : 'G')}
                </Avatar>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="caption" color="text.secondary">
                    {msg.playerId ? 'Player' : 'AI-GM'} · {new Date(msg.createdAt).toLocaleTimeString()}
                    {isWhisper(msg) && <Chip label="Whisper" size="small" sx={{ ml: 1, height: 16, fontSize: 9 }} color="warning" />}
                  </Typography>
                  <Typography variant="body2" sx={{ fontStyle: isWhisper(msg) ? 'italic' : 'normal' }}>
                    {msg.content}
                  </Typography>
                </Box>
              </Box>
              <Divider sx={{ my: 1 }} />
            </Box>
          ))
        )}
        <div ref={messagesEndRef} />
      </Box>

      {/* Input */}
      <Box sx={{ p: 1, borderTop: 1, borderColor: 'divider' }}>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            fullWidth
            size="small"
            placeholder="In-game public message (visible to all)..."
            value={message}
            onChange={e => setMessage(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && onSend()}
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={onSend} size="small">
                    <SendIcon />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
          {isCreator && whisperTarget && (
            <Button
              variant="contained"
              onClick={() => { onInGameWhisperToPlayer(whisperTarget); setWhisperTarget(''); }}
              disabled={!whisperContent.trim()}
              startIcon={<MicIcon fontSize="small" />}
              sx={{ minWidth: 120 }}
            >
              Whisper
            </Button>
          )}
        </Box>
        {isCreator && whisperContent && !whisperTarget && (
          <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
            Select a player above to whisper to, or type a public message.
          </Typography>
        )}
      </Box>
    </Paper>
  );
}

function PlayersTab({ players }: { players: any[] }) {
  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>Players ({players.length})</Typography>
      <List>
        {players.map((p: any) => (
          <ListItem key={p.id} sx={{ px: 0 }}>
            <ListItemAvatar>
              <Avatar sx={{ bgcolor: p.role === 'Creator' ? 'warning.main' : p.role === 'Spectator' ? 'info.main' : 'primary.main' }}>
                {p.role === 'Creator' ? '🎬' : p.role === 'Spectator' ? '👁️' : '👤'}
              </Avatar>
            </ListItemAvatar>
            <ListItemText
              primary={p.userName || p.characterName}
              secondary={p.characterName}
            />
            <Chip label={p.role} size="small" color={p.role === 'Creator' ? 'warning' : p.role === 'Spectator' ? 'info' : 'default'} variant="outlined" />
            <Chip label={p.status} size="small" color={p.status === 'Active' ? 'success' : 'default'} />
          </ListItem>
        ))}
      </List>
    </Paper>
  );
}

function CharactersTab({ characters }: { characters: any[] }) {
  const navigate = useNavigate();
  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>Characters ({characters.length})</Typography>
      {characters.length === 0 ? (
        <Typography color="text.secondary">No characters yet. Characters will appear when players create them.</Typography>
      ) : (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          {characters.map((c: any) => (
            <Paper key={c.id} variant="outlined" sx={{ p: 2 }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                <Typography variant="h6">{c.name}</Typography>
                <Chip label={`${c.class} Lv.${c.level}`} size="small" />
              </Box>
              <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
                <Chip label={`HP: ${c.currentHP}/${c.maxHP}`} size="small" color={c.currentHP < c.maxHP * 0.3 ? 'error' : 'default'} />
                <Chip label={c.playerName} size="small" variant="outlined" />
              </Box>
              <Typography variant="caption" color="text.secondary">
                Last updated: {new Date(c.updatedAt).toLocaleString()}
              </Typography>
              <Box sx={{ mt: 1, display: 'flex', gap: 1 }}>
                <Button size="small" variant="outlined" startIcon={<SheetIcon />}
                  onClick={() => navigate(`/character/${c.id}`)}>
                  View Sheet
                </Button>
              </Box>
            </Paper>
          ))}
        </Box>
      )}
    </Paper>
  );
}

function ActionsTab({ onSkillCheck, onAttack, onDiceRoll }: any) {
  const [skill, setSkill] = useState('Perception');
  const [dc, setDc] = useState(15);
  const [weapon, setWeapon] = useState('Longsword');
  const [target, setTarget] = useState('');

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>Quick Actions</Typography>

      <Box sx={{ mb: 3 }}>
        <Typography variant="subtitle1" gutterBottom>🎲 Dice Roller</Typography>
        <Button variant="contained" onClick={onDiceRoll}>Open Dice Roller</Button>
      </Box>

      <Divider sx={{ my: 2 }} />

      <Box sx={{ mb: 3 }}>
        <Typography variant="subtitle1" gutterBottom>📋 Skill Check</Typography>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
          <TextField size="small" label="Skill" value={skill} onChange={e => setSkill(e.target.value)} sx={{ minWidth: 150 }} />
          <TextField size="small" label="DC" type="number" value={dc} onChange={e => setDc(parseInt(e.target.value) || 0)} sx={{ width: 80 }} />
          <Button variant="outlined" onClick={() => onSkillCheck(skill, dc)}>Roll {skill}</Button>
        </Box>
      </Box>

      <Divider sx={{ my: 2 }} />

      <Box>
        <Typography variant="subtitle1" gutterBottom>⚔️ Attack</Typography>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
          <TextField size="small" label="Weapon" value={weapon} onChange={e => setWeapon(e.target.value)} sx={{ minWidth: 150 }} />
          <TextField size="small" label="Target" value={target} onChange={e => setTarget(e.target.value)} placeholder="Target name" />
          <Button variant="outlined" color="error" onClick={() => onAttack(weapon, target || 'target')}>Attack</Button>
        </Box>
      </Box>
    </Paper>
  );
}

function SettingsTab({ game, sessions, onNewSession }: any) {
  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>Game Settings</Typography>

      <Box sx={{ mb: 3 }}>
        <Typography variant="subtitle1">Game Info</Typography>
        <Typography variant="body2">Name: {game.name}</Typography>
        <Typography variant="body2">System: {game.systemId} v{game.systemVersion || 'unknown'}</Typography>
        <Typography variant="body2">Status: {game.status}</Typography>
        <Typography variant="body2">Created: {new Date(game.createdAt).toLocaleDateString()}</Typography>
      </Box>

      <Divider sx={{ my: 2 }} />

      <Box>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
          <Typography variant="subtitle1">Sessions ({sessions?.length || 0})</Typography>
          <Button size="small" variant="outlined" onClick={onNewSession}>New Session</Button>
        </Box>
        {sessions && sessions.length > 0 ? (
          <List>
            {sessions.map((s: any) => (
              <ListItem key={s.id} sx={{ px: 0 }}>
                <ListItemText
                  primary={s.title}
                  secondary={`${s.messageCount} messages · ${new Date(s.startedAt).toLocaleDateString()}`}
                />
                <Chip label={s.endedAt ? 'Closed' : 'Active'} size="small" color={s.endedAt ? 'default' : 'success'} />
              </ListItem>
            ))}
          </List>
        ) : (
          <Typography color="text.secondary" variant="body2">No sessions yet. Create one to start tracking your game.</Typography>
        )}
      </Box>
    </Paper>
  );
}

// ==================== Whispers Tab ====================

function WhispersTab({ whispers, whisperContent, setWhisperContent, players, messagesEndRef, isCreator, onInGameWhisperToPlayer, onOOCWhisperToPlayer }: any) {
  const [activeWhisperTab, setActiveWhisperTab] = useState(0); // 0 = In-game, 1 = OOC
  const [whisperTarget, setWhisperTarget] = useState('');

  const isIngameWhisper = (w: WhisperResponse) => {
    return w.type === WhisperType.InGamePlayerToGM || w.type === WhisperType.InGameGMToPlayer;
  };

  const isOOCWhisper = (w: WhisperResponse) => {
    return w.type === WhisperType.OOCPlayerToGM || w.type === WhisperType.OOCGMToPlayer;
  };

  const getWhisperTypeLabel = (type: number) => {
    switch (type) {
      case WhisperType.InGamePlayerToGM: return '🤫 Player→GM (In-Game)';
      case WhisperType.InGameGMToPlayer: return '🤫 GM→Player (In-Game)';
      case WhisperType.OOCPlayerToGM: return '🤫 Player→GM (OOC)';
      case WhisperType.OOCGMToPlayer: return '🤫 GM→Player (OOC)';
      case WhisperType.PlayerToPlayer: return '🤫 Player→Player';
      case WhisperType.GMToGroup: return '🤫 GM→Group';
      case WhisperType.GMToAll: return '🤫 GM→All';
      default: return '🤫 Whisper';
    }
  };

  const getWhisperChipColor = (type: number) => {
    switch (type) {
      case WhisperType.InGamePlayerToGM:
      case WhisperType.InGameGMToPlayer: return 'primary';
      case WhisperType.OOCPlayerToGM:
      case WhisperType.OOCGMToPlayer: return 'info';
      default: return 'default';
    }
  };

  return (
    <Paper sx={{ height: '70vh', display: 'flex', flexDirection: 'column' }}>
      {/* Whisper type tabs */}
      <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
        <Tabs value={activeWhisperTab} onChange={(_, v) => setActiveWhisperTab(v)}>
          <Tab label={`🎮 In-Game Whispers (${whispers.filter(isIngameWhisper).length})`} />
          <Tab label={`📢 OOC Whispers (${whispers.filter(isOOCWhisper).length})`} />
        </Tabs>
      </Box>

      {/* GM whisper target selector */}
      {isCreator && (
        <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography variant="caption" color="text.secondary">
            {activeWhisperTab === 0 ? 'In-game whisper to:' : 'OOC whisper to:'}
          </Typography>
          {players.filter((p: any) => p.status === 'Active').map((p: any) => (
            <Chip
              key={p.id}
              label={p.characterName}
              size="small"
              clickable
              onClick={() => setWhisperTarget(p.id)}
              sx={{ m: 0.25 }}
            />
          ))}
        </Box>
      )}

      {/* Whispers */}
      <Box sx={{ flex: 1, overflow: 'auto', p: 2 }}>
        {whispers.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 8 }}>
            No whispers yet. Whispers are private messages visible only to the sender and recipients.
          </Typography>
        ) : (
          whispers
            .filter((w: WhisperResponse) => activeWhisperTab === 0 ? isIngameWhisper(w) : isOOCWhisper(w))
            .map((w: any, i: number) => (
              <Box key={i} sx={{ mb: 1 }}>
                <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
                  <Avatar sx={{ width: 24, height: 24, fontSize: 12, bgcolor: w.isSent ? 'success.main' : 'warning.main' }}>
                    {w.isSent ? '📤' : '📥'}
                  </Avatar>
                  <Box sx={{ flex: 1 }}>
                    <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
                      <Typography variant="caption" color="text.secondary">
                        {w.fromCharacter}
                      </Typography>
                      <Chip
                        label={getWhisperTypeLabel(w.type)}
                        size="small"
                        sx={{ height: 16, fontSize: 9 }}
                        color={getWhisperChipColor(w.type) as any}
                      />
                      <Typography variant="caption" color="text.secondary">
                        {new Date(w.createdAt).toLocaleTimeString()}
                      </Typography>
                    </Box>
                    <Typography variant="body2" sx={{ fontStyle: 'italic', color: 'text.primary' }}>
                      {w.content}
                    </Typography>
                  </Box>
                </Box>
                <Divider sx={{ my: 1 }} />
              </Box>
            ))
        )}
        <div ref={messagesEndRef as any} />
      </Box>

      {/* Input */}
      <Box sx={{ p: 1, borderTop: 1, borderColor: 'divider' }}>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            fullWidth
            size="small"
            placeholder={
              activeWhisperTab === 0
                ? whisperTarget ? 'In-game whisper content...' : 'Type a whisper...'
                : whisperTarget ? 'OOC whisper content...' : 'Type an OOC whisper...'
            }
            value={whisperContent}
            onChange={e => setWhisperContent(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && whisperTarget && (
              activeWhisperTab === 0
                ? onInGameWhisperToPlayer?.(whisperTarget)
                : onOOCWhisperToPlayer?.(whisperTarget)
            )}
          />
          {isCreator && whisperTarget && (
            <Button
              variant="contained"
              onClick={() => {
                if (activeWhisperTab === 0) {
                  onInGameWhisperToPlayer?.(whisperTarget);
                } else {
                  onOOCWhisperToPlayer?.(whisperTarget);
                }
                setWhisperTarget('');
              }}
              disabled={!whisperContent.trim()}
              startIcon={<MicIcon fontSize="small" />}
              sx={{ minWidth: 120 }}
            >
              {activeWhisperTab === 0 ? 'In-Game Whisper' : 'OOC Whisper'}
            </Button>
          )}
        </Box>
        {isCreator && !whisperTarget && (
          <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
            Select a player above to whisper to.
          </Typography>
        )}
      </Box>
    </Paper>
  );
}

// ==================== OOC Chat Tab ====================

function OOCTab({ messages, oocMessage, setOocMessage, onSendOOC, oocWhisperContent, setOocWhisperContent, onSendOOCWhisper, oocWhispers, messagesEndRef, sessions, isCreator, players, onOOCWhisperToPlayer }: any) {
  const [showOOCWhisperTarget, setShowOOCWhisperTarget] = useState(false);
  const [oocWhisperTarget, setOocWhisperTarget] = useState('');
  const [showOOCWhisperHistory, setShowOOCWhisperHistory] = useState(false);

  const activeSession = sessions?.find((s: any) => !s.endedAt);

  return (
    <Paper sx={{ height: '70vh', display: 'flex', flexDirection: 'column' }}>
      {/* Toolbar */}
      <Box sx={{ p: 1, display: 'flex', gap: 1, borderBottom: 1, borderColor: 'divider', flexWrap: 'wrap' }}>
        <Chip label="📢 OOC" size="small" color="info" variant="outlined" />
        <Button size="small" onClick={() => setShowOOCWhisperHistory(!showOOCWhisperHistory)}>
          🤫 OOC Whispers ({oocWhispers.length})
        </Button>
        {isCreator && (
          <Button size="small" variant="outlined" onClick={() => setShowOOCWhisperTarget(!showOOCWhisperTarget)}>
            🤫 OOC to Player
          </Button>
        )}
        {activeSession && (
          <Chip label={`Session: ${activeSession.title}`} size="small" color="info" variant="outlined" />
        )}
      </Box>

      {/* OOC Whisper target selector */}
      {showOOCWhisperTarget && (
        <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography variant="caption" color="text.secondary">OOC whisper to player:</Typography>
          {players.filter((p: any) => p.status === 'Active').map((p: any) => (
            <Chip
              key={p.id}
              label={p.characterName}
              size="small"
              clickable
              onClick={() => { setOocWhisperTarget(p.id); setShowOOCWhisperTarget(false); }}
              sx={{ m: 0.25 }}
            />
          ))}
        </Box>
      )}

      {/* OOC Whisper history */}
      <Collapse in={showOOCWhisperHistory}>
        <Box sx={{ p: 1, maxHeight: 200, overflow: 'auto', bgcolor: 'background.default', borderBottom: 1, borderColor: 'divider' }}>
          <Typography variant="caption" color="text.secondary">OOC Whisper History:</Typography>
          {oocWhispers.length === 0 ? (
            <Typography variant="caption" color="text.secondary">No OOC whispers yet.</Typography>
          ) : (
            oocWhispers.map((w: any, i: number) => (
              <Box key={i} sx={{ mb: 0.5 }}>
                <Typography variant="caption" sx={{ display: 'block' }}>
                  <strong>{w.fromCharacter}</strong> → {w.targets === 'gm' ? 'GM' : `player:${w.targets}`}
                  {' '}{new Date(w.createdAt).toLocaleTimeString()}
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontStyle: 'italic' }}>
                  {w.content}
                </Typography>
              </Box>
            ))
          )}
        </Box>
      </Collapse>

      {/* OOC Messages */}
      <Box sx={{ flex: 1, overflow: 'auto', p: 2 }}>
        {messages.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 8 }}>
            No OOC messages yet. This channel is for out-of-character discussion.
          </Typography>
        ) : (
          messages.map((msg: any, i: number) => (
            <Box key={i} sx={{ mb: 1 }}>
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
                <Avatar sx={{ width: 24, height: 24, fontSize: 12, bgcolor: 'info.main' }}>
                  📢
                </Avatar>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="caption" color="text.secondary">
                    {msg.playerId ? 'Player' : 'AI-GM'} · {new Date(msg.createdAt).toLocaleTimeString()}
                    <Chip label="OOC" size="small" sx={{ ml: 1, height: 16, fontSize: 9 }} color="info" />
                  </Typography>
                  <Typography variant="body2" sx={{ color: 'info.main' }}>
                    {msg.content}
                  </Typography>
                </Box>
              </Box>
              <Divider sx={{ my: 1 }} />
            </Box>
          ))
        )}
        <div ref={messagesEndRef} />
      </Box>

      {/* Input */}
      <Box sx={{ p: 1, borderTop: 1, borderColor: 'divider' }}>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            fullWidth
            size="small"
            placeholder="OOC message (out of character, never influences narrative)..."
            value={oocMessage}
            onChange={e => setOocMessage(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && onSendOOC()}
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={onSendOOC} size="small">
                    <SendIcon />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
          {/* OOC Whisper to player (GM only) */}
          {isCreator && oocWhisperTarget && (
            <Button
              variant="contained"
              color="info"
              onClick={() => { onOOCWhisperToPlayer(oocWhisperTarget); setOocWhisperTarget(''); }}
              disabled={!oocWhisperContent.trim()}
              startIcon={<MicIcon fontSize="small" />}
              sx={{ minWidth: 120 }}
            >
              OOC Whisper
            </Button>
          )}
        </Box>
        {/* OOC Whisper input */}
        <Box sx={{ mt: 1, display: 'flex', gap: 1 }}>
          <TextField
            size="small"
            placeholder="OOC whisper to GM (private)..."
            value={oocWhisperContent}
            onChange={e => setOocWhisperContent(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && onSendOOCWhisper()}
            fullWidth
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={onSendOOCWhisper} size="small">
                    <MicIcon fontSize="small" />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
        </Box>
        {isCreator && oocWhisperContent && !oocWhisperTarget && (
          <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
            Select a player above to OOC whisper to, or type a public OOC message.
          </Typography>
        )}
      </Box>
    </Paper>
  );
}

// ==================== Agent Calls Tab ====================

function AgentCallsTab({ calls, onRefresh }: any) {
  const getStatusColor = (status: number) => {
    switch (status) {
      case AgentCallStatus.Running: return 'warning';
      case AgentCallStatus.Completed: return 'success';
      case AgentCallStatus.Failed: return 'error';
      case AgentCallStatus.Pending: return 'default';
      default: return 'default';
    }
  };

  const getAgentLabel = (agent: number) => {
    switch (agent) {
      case AgentType.GM: return '🤖 AI-GM';
      case AgentType.LLM: return '🤖 LLM';
      case AgentType.Dice: return '🎲 Dice';
      case AgentType.RAG: return '📚 RAG';
      case AgentType.NPC: return '🧙 NPC';
      case AgentType.Player: return '👤 Player';
      case AgentType.System: return '⚙️ System';
      default: return '❓ Unknown';
    }
  };

  const getActionLabel = (action: number) => {
    switch (action) {
      case AgentAction.Query: return 'Query';
      case AgentAction.Generate: return 'Generate';
      case AgentAction.Roll: return 'Roll';
      case AgentAction.Check: return 'Check';
      case AgentAction.Narrate: return 'Narrate';
      case AgentAction.Suggest: return 'Suggest';
      case AgentAction.Execute: return 'Execute';
      case AgentAction.Notify: return 'Notify';
      case AgentAction.Recall: return 'Recall';
      case AgentAction.ManageState: return 'State';
      default: return 'Unknown';
    }
  };

  return (
    <Paper sx={{ height: '70vh', display: 'flex', flexDirection: 'column' }}>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Agent Call History ({calls.length})</Typography>
        <Button size="small" variant="outlined" onClick={onRefresh} startIcon={<ReplayIcon fontSize="small" />}>
          Refresh
        </Button>
      </Box>
      <Divider />
      <Box sx={{ flex: 1, overflow: 'auto', p: 2 }}>
        {calls.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 8 }}>
            No agent calls yet. Agent calls are made internally by the game system.
          </Typography>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>From</TableCell>
                  <TableCell>To</TableCell>
                  <TableCell>Action</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Duration</TableCell>
                  <TableCell>Time</TableCell>
                  <TableCell>Output</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {calls.map((call: any, i: number) => (
                  <TableRow key={i} sx={{ '&:nth-of-type(odd)': { bgcolor: 'action.hover' } }}>
                    <TableCell>{getAgentLabel(call.fromAgent)}</TableCell>
                    <TableCell>{getAgentLabel(call.toAgent)}</TableCell>
                    <TableCell>{getActionLabel(call.action)}</TableCell>
                    <TableCell>
                      <Chip
                        label={call.status}
                        size="small"
                        color={getStatusColor(call.status) as any}
                        variant="outlined"
                      />
                    </TableCell>
                    <TableCell>{call.durationMs}ms</TableCell>
                    <TableCell>{new Date(call.createdAt).toLocaleTimeString()}</TableCell>
                    <TableCell sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {call.outputMessage || call.output?.substring(0, 50) || '-'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Box>
    </Paper>
  );
}
