import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../api/authHook';
import { useGame, useSessions, usePlayers, useCharacters } from '../api/gameHooks';
import { useGameHub } from '../api/hubHook';
import { api } from '../api/client';
import { WhisperType, AgentType, AgentAction, AgentCallStatus, MessageType } from '../types';
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
  People as PeopleIcon, MenuBook as BookIcon, Settings as SettingsIcon,
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
  const { isConnected, connect, on, invoke, disconnect } = useGameHub();

  const [activeTab, setActiveTab] = useState(0);
  const [message, setMessage] = useState('');
  const [messages, setMessages] = useState<any[]>([]);
  const [whispers, setWhispers] = useState<any[]>([]);
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
  const [whisperTargets, setWhisperTargets] = useState('all');
  const [whisperContent, setWhisperContent] = useState('');
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
      setMessages(prev => [...prev, msg]);
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
      setMessages(prev => [...prev, {
        id: Date.now(),
        type: 'SkillCheck',
        content: `${result.Skill}: d20(${result.DiceRoll})+${result.Modifier}=${result.Total} vs DC ${result.DC} -> ${result.Success ? 'SUCCESS' : 'FAILURE'}`,
        timestamp: result.RolledAt,
        isSystem: true
      }]);
    });

    on('AttackResult', (result: any) => {
      setMessages(prev => [...prev, {
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
  }, [messages]);

  // Load active combats
  useEffect(() => {
    if (id && isConnected) {
      api.getActiveCombats(id).then((combats: unknown) => {
        const c = combats as any[];
        if (c && c.length > 0) setActiveCombats(c);
      }).catch(() => {});
    }
  }, [id, isConnected]);

  const handleSendMessage = async () => {
    if (!message.trim() || !selectedSession) return;

    try {
      await invoke('SendMessage', selectedSession, message, MessageType.Chat);
      setMessage('');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleSendWhisper = async () => {
    if (!whisperContent.trim() || !whisperTargets) return;

    try {
      await invoke('SendWhisper', whisperTargets, whisperContent);
      setWhisperContent('');
      setWhisperTargets('all');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleSendGMWhisper = async (targetPlayerId: string) => {
    if (!whisperContent.trim()) return;

    try {
      await invoke('SendGMWhisper', targetPlayerId, whisperContent);
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
    { label: 'Chat', icon: <SendIcon /> },
    { label: 'Combat', icon: <CombatIcon />, count: activeCombats.length > 0 ? activeCombats.length : undefined },
    { label: 'Whispers', icon: <ChatBubbleIcon />, count: whispers.length },
    { label: 'Players', icon: <PeopleIcon />, count: players.length },
    { label: 'Characters', icon: <BookIcon />, count: characters.length },
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
          <Box sx={{ display: 'flex', gap: 1, mt: 0.5 }}>
            <Chip label={game.systemId} size="small" />
            <Chip label={game.status} size="small" color={game.status === 'Active' ? 'success' : 'default'} />
            {isConnected && <Chip label="Connected" size="small" color="success" icon={<ReplayIcon fontSize="small" />}></Chip>}
            {!isConnected && <Chip label="Disconnected" size="small" color="error" />}</Box>
        </Box>
        <Button variant="outlined" color="error" startIcon={<LeaveIcon />} onClick={handleLeave}>
          Leave
        </Button>
      </Box>

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
              messages={messages}
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
            />
          )}

          {activeTab === 1 && (
            <CombatTab gameId={id || ''} />
          )}

          {activeTab === 2 && (
            <WhispersTab
              whispers={whispers}
              whisperContent={whisperContent}
              setWhisperContent={setWhisperContent}
              whisperTargets={whisperTargets}
              setWhisperTargets={setWhisperTargets}
              onSendWhisper={handleSendWhisper}
              onSendGMWhisper={handleSendGMWhisper}
              players={players}
              messagesEndRef={messagesEndRef}
            />
          )}

          {activeTab === 3 && (
            <PlayersTab players={players} />
          )}

          {activeTab === 4 && (
            <CharactersTab characters={characters} />
          )}

          {activeTab === 5 && (
            <ActionsTab onSkillCheck={handleSkillCheck} onAttack={handleAttack} onDiceRoll={() => setOpenDiceDialog(true)} />
          )}

          {activeTab === 6 && (
            <AgentCallsTab
              calls={agentCalls}
              onRefresh={handleRefreshAgentCalls}
            />
          )}

          {activeTab === 7 && (
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

function ChatTab({ messages, message, setMessage, onSend, onDiceRoll, onSkillCheck, diceResults, showDiceHistory, setShowDiceHistory, messagesEndRef, selectedSession, sessions }: any) {
  const filteredMessages = selectedSession
    ? messages.filter((m: any) => m.sessionId === selectedSession)
    : messages;
  const activeSession = sessions?.find((s: any) => !s.endedAt);
  return (
    <Paper sx={{ height: '70vh', display: 'flex', flexDirection: 'column' }}>
      {/* Toolbar */}
      <Box sx={{ p: 1, display: 'flex', gap: 1, borderBottom: 1, borderColor: 'divider', flexWrap: 'wrap' }}>
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
        {activeSession && (
          <Chip label={`Session: ${activeSession.title}`} size="small" color="primary" variant="outlined" />
        )}
      </Box>

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
              {msg.type === 'Dice' && (
                <Chip label={`🎲 ${msg.content}`} size="small" sx={{ mb: 0.5 }} color="primary" variant="outlined" />
              )}
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
                <Avatar sx={{ width: 24, height: 24, fontSize: 12 }}>
                  {msg.playerId ? 'P' : 'G'}
                </Avatar>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="caption" color="text.secondary">
                    {msg.playerId ? 'Player' : 'GM'} · {new Date(msg.createdAt).toLocaleTimeString()}
                  </Typography>
                  <Typography variant="body2">{msg.content}</Typography>
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
        <TextField
          fullWidth
          size="small"
          placeholder="Type a message..."
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
              <Avatar sx={{ bgcolor: p.role === 'GM' ? 'error.main' : 'primary.main' }}>
                {p.role === 'GM' ? '👑' : '👤'}
              </Avatar>
            </ListItemAvatar>
            <ListItemText
              primary={p.userName || p.characterName}
              secondary={p.characterName}
            />
            <Chip label={p.role} size="small" color={p.role === 'GM' ? 'error' : 'default'} variant="outlined" />
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

function WhispersTab({ whispers, whisperContent, setWhisperContent, whisperTargets, setWhisperTargets, onSendWhisper, onSendGMWhisper, players, messagesEndRef }: any) {
  const [showGMWhisper, setShowGMWhisper] = useState(false);
  const [gmTarget, setGmTarget] = useState('');

  const isGM = players.some((p: any) => p.role === 'GM');

  const getWhisperTypeLabel = (type: number) => {
    switch (type) {
      case WhisperType.PlayerToPlayer: return '🤫 Player→Player';
      case WhisperType.PlayerToGM: return '🤫 Player→GM';
      case WhisperType.GMToPlayer: return '🤫 GM→Player';
      case WhisperType.GMToGroup: return '🤫 GM→Group';
      case WhisperType.GMToAll: return '🤫 GM→All';
      default: return '🤫 Whisper';
    }
  };

  return (
    <Paper sx={{ height: '70vh', display: 'flex', flexDirection: 'column' }}>
      {/* Toolbar */}
      <Box sx={{ p: 1, display: 'flex', gap: 1, borderBottom: 1, borderColor: 'divider', flexWrap: 'wrap' }}>
        <TextField
          size="small"
          label="Targets"
          value={whisperTargets}
          onChange={e => setWhisperTargets(e.target.value)}
          placeholder="player:{id}, all, group:{name}"
          sx={{ minWidth: 200 }}
        />
        {isGM && (
          <Button size="small" variant="outlined" onClick={() => setShowGMWhisper(!showGMWhisper)}>
            GM Whisper Mode
          </Button>
        )}
      </Box>

      {/* GM Whisper Target Selector */}
      {showGMWhisper && (
        <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          <Typography variant="caption">Target:</Typography>
          {players.filter((p: any) => p.status === 'Active').map((p: any) => (
            <Chip
              key={p.id}
              label={p.characterName}
              size="small"
              clickable
              onClick={() => { setGmTarget(p.id); setShowGMWhisper(false); }}
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
          whispers.map((w: any, i: number) => (
            <Box key={i} sx={{ mb: 1 }}>
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
                <Avatar sx={{ width: 24, height: 24, fontSize: 12, bgcolor: 'warning.main' }}>
                  🔇
                </Avatar>
                <Box sx={{ flex: 1 }}>
                  <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                    <Typography variant="caption" color="text.secondary">
                      {w.fromCharacter}
                    </Typography>
                    <Chip label={getWhisperTypeLabel(w.type)} size="small" sx={{ height: 16, fontSize: 10 }} />
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
            placeholder={showGMWhisper ? "Type a GM whisper..." : "Type a whisper..."}
            value={whisperContent}
            onChange={e => setWhisperContent(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && onSendWhisper()}
          />
          <Button
            variant="contained"
            onClick={showGMWhisper && gmTarget ? () => onSendGMWhisper(gmTarget) : onSendWhisper}
            disabled={!whisperContent.trim()}
            startIcon={<MicIcon fontSize="small" />}
          >
            {showGMWhisper && gmTarget ? 'Send to Player' : 'Whisper'}
          </Button>
        </Box>
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
      case AgentType.GM: return '🎭 GM';
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
                      {call.outputMessage || call.output?.substring(0, 50) || '—'}
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
