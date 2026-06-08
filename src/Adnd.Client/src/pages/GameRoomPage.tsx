import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../api/authHook';
import { useGame, useSessions, usePlayers, useCharacters, useGMStatus, useSway } from '../api/gameHooks';
import { useGameHub } from '../api/hubHook';
import { useToolCalls } from '../api/toolCallsHook';
import { api } from '../api/client';
import { WhisperType, AgentType, AgentAction, AgentCallStatus } from '../types';
import CombatTab from './CombatTab';
import CharacterCreateWizard from './CharacterCreateWizard';
import ToolCallBanner from '../components/ToolCallBanner';
import PlayerRollDialog from '../components/PlayerRollDialog';
import {
  Container, Box, Typography, Paper, TextField, Button, Tabs, Tab,
  List, ListItem, ListItemText, ListItemAvatar, Avatar, Chip,
  Dialog, DialogTitle, DialogContent, DialogActions,
  IconButton, Divider, Alert, AlertTitle, Collapse,
  InputAdornment, MenuItem, Select, FormControl,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  InputLabel
} from '@mui/material';
import { Send as SendIcon, DirectionsRun as ActionIcon, SportsEsports as DiceIcon,
  People as PeopleIcon, Settings as SettingsIcon,
  Replay as ReplayIcon, ExitToApp as LeaveIcon,
  Chat as ChatBubbleIcon, Article as SheetIcon,
  DirectionsRun as CombatIcon } from '@mui/icons-material';

// ==================== Unified Message Types ====================

type UnifiedMessageType =
  | 'inGamePublic'   // In-game public (narrative)
  | 'inGameWhisper'  // In-game whisper (GM knowledge / divination)
  | 'oocPublic'      // OOC public
  | 'oocWhisper'     // OOC whisper
  | 'dice'           // Dice roll
  | 'skillCheck'     // Skill check result
  | 'attack'         // Attack result
  | 'system'         // System notification (join/leave, etc.)
  | 'agentCall'      // Agent call log
  | 'agentResponse'; // Agent response

interface UnifiedMessage {
  id: string | number;
  type: UnifiedMessageType;
  content: string;
  senderName: string;
  senderRole: string;
  timestamp: string;
  isSystem?: boolean;
  isWhisper?: boolean;
  whisperTo?: string;
  diceFormula?: string;
  diceTotal?: number;
  diceRolls?: number[];
  skill?: string;
  skillDC?: number;
  skillResult?: string;
  attackWeapon?: string;
  attackTarget?: string;
  attackHit?: boolean;
  attackDamage?: number;
  agentFrom?: string;
  agentAction?: string;
  agentStatus?: string;
  extra?: React.ReactNode;
}

// ==================== Message Color Config ====================

const MESSAGE_STYLES: Record<UnifiedMessageType, {
  bg: string;
  border: string;
  chipColor: 'primary' | 'info' | 'warning' | 'success' | 'error' | 'default';
  chipLabel: string;
  chipIcon: string;
}> = {
  inGamePublic:   { bg: 'rgba(103, 194, 58, 0.06)',   border: 'rgba(103, 194, 58, 0.25)',  chipColor: 'success', chipLabel: 'In-Game', chipIcon: '🎮' },
  inGameWhisper:  { bg: 'rgba(255, 193, 7, 0.08)',    border: 'rgba(255, 193, 7, 0.35)',  chipColor: 'warning', chipLabel: 'Whisper', chipIcon: '🤫' },
  oocPublic:      { bg: 'rgba(33, 150, 243, 0.06)',   border: 'rgba(33, 150, 243, 0.25)',  chipColor: 'info',    chipLabel: 'OOC', chipIcon: '📢' },
  oocWhisper:     { bg: 'rgba(156, 39, 176, 0.06)',   border: 'rgba(156, 39, 176, 0.25)',  chipColor: 'info', chipLabel: 'OOC Whisper', chipIcon: '🤫' },
  dice:           { bg: 'rgba(255, 152, 0, 0.05)',    border: 'rgba(255, 152, 0, 0.20)',  chipColor: 'default', chipLabel: '🎲 Dice', chipIcon: '🎲' },
  skillCheck:     { bg: 'rgba(156, 39, 176, 0.05)',   border: 'rgba(156, 39, 176, 0.20)',  chipColor: 'info', chipLabel: '📋 Check', chipIcon: '📋' },
  attack:         { bg: 'rgba(244, 67, 54, 0.05)',    border: 'rgba(244, 67, 54, 0.20)',  chipColor: 'error', chipLabel: '⚔️ Attack', chipIcon: '⚔️' },
  system:         { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: 'System', chipIcon: '⚙️' },
  agentCall:      { bg: 'rgba(121, 85, 72, 0.05)',    border: 'rgba(121, 85, 72, 0.20)',  chipColor: 'default', chipLabel: '🤖 Agent', chipIcon: '🤖' },
  agentResponse:  { bg: 'rgba(76, 175, 80, 0.05)',    border: 'rgba(76, 175, 80, 0.20)',  chipColor: 'success', chipLabel: '🤖 Reply', chipIcon: '🤖' },
};

// ==================== Message Input Types ====================

type MessageInputType = 'inGame' | 'ooc';
type MessageTarget = 'all' | 'gm' | 'player';

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
  const [selectedSession, setSelectedSession] = useState<string | null>(null);
  const [openDiceDialog, setOpenDiceDialog] = useState(false);
  const [diceFormula, setDiceFormula] = useState('1d20');
  const [showDiceHistory, setShowDiceHistory] = useState(false);
  const [createSessionOpen, setCreateSessionOpen] = useState(false);
  const [sessionTitle, setSessionTitle] = useState('');
  const [errorState, setErrorState] = useState<string | null>(null);
  const [successState, setSuccessState] = useState<string | null>(null);
  const [activeCombats, setActiveCombats] = useState<any[]>([]);
  const [showCharacterWizard, setShowCharacterWizard] = useState(false);

  // ==================== Tool Call State ====================
  const { pendingCalls, refetch: refetchToolCalls, confirmToolCall, confirmPlayerRoll, declinePlayerRoll } = useToolCalls(id);
  const [showRollDialog, setShowRollDialog] = useState(false);
  const [rollSkill, setRollSkill] = useState('');
  const [rollFormula, setRollFormula] = useState('1d20');
  const [rollDC, setRollDC] = useState(15);
  const [rollContext, setRollContext] = useState('');
  const [rollOptional, setRollOptional] = useState(false);

  // Unified chat state
  const [messages, setMessages] = useState<UnifiedMessage[]>([]);
  const [inputType, setInputType] = useState<MessageInputType>('inGame');
  const [inputTarget, setInputTarget] = useState<MessageTarget>('all');
  const [whisperTargetPlayer, setWhisperTargetPlayer] = useState<string | null>(null);
  const [whisperInput, setWhisperInput] = useState('');
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

  // Listen for incoming messages — all funnel into unified chat
  useEffect(() => {
    if (!isConnected) return;

    on('NewMessage', (msg: any) => {
      setMessages(prev => [...prev, {
        id: msg.Id,
        type: 'inGamePublic',
        content: msg.Content,
        senderName: msg.PlayerId ? 'Player' : 'AI-GM',
        senderRole: msg.PlayerId ? 'Player' : 'GM',
        timestamp: msg.CreatedAt,
      }]);
    });

    on('NewOOCMessage', (msg: any) => {
      setMessages(prev => [...prev, {
        id: `ooc-${msg.Id}`,
        type: 'oocPublic',
        content: msg.Content,
        senderName: msg.PlayerId ? 'Player' : 'AI-GM',
        senderRole: msg.PlayerId ? 'Player' : 'GM',
        timestamp: msg.CreatedAt,
      }]);
    });

    on('NewWhisper', (whisper: any) => {
      const isInGame = whisper.Type === WhisperType.InGamePlayerToGM ||
                       whisper.Type === WhisperType.InGameGMToPlayer;
      setMessages(prev => [...prev, {
        id: `whisper-${whisper.Id}`,
        type: isInGame ? 'inGameWhisper' : 'oocWhisper',
        content: whisper.Content,
        senderName: whisper.FromCharacter,
        senderRole: whisper.FromRole,
        timestamp: whisper.CreatedAt,
        isWhisper: true,
        whisperTo: whisper.Targets === 'gm' ? 'GM' : whisper.Targets,
      }]);
    });

    on('NewOOCWhisper', (whisper: any) => {
      setMessages(prev => [...prev, {
        id: `ooc-whisper-${whisper.Id}`,
        type: 'oocWhisper',
        content: whisper.Content,
        senderName: whisper.FromCharacter,
        senderRole: whisper.FromRole,
        timestamp: whisper.CreatedAt,
        isWhisper: true,
        whisperTo: whisper.Targets === 'gm' ? 'GM' : whisper.Targets,
      }]);
    });

    on('DiceRollResult', (result: any) => {
      setMessages(prev => [...prev, {
        id: `dice-${Date.now()}`,
        type: 'dice',
        content: `${result.Formula} → ${result.Total}`,
        senderName: result.PlayerId ? 'Player' : 'System',
        senderRole: result.PlayerId ? 'Player' : 'System',
        timestamp: result.Timestamp,
        isSystem: true,
        diceFormula: result.Formula,
        diceTotal: result.Total,
        diceRolls: result.Rolls,
      }]);
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
        id: `skill-${Date.now()}`,
        type: 'skillCheck',
        content: `${result.Skill}: d20(${result.DiceRoll})+${result.Modifier}=${result.Total} vs DC ${result.DC} → ${result.Success ? '✓ SUCCESS' : '✗ FAILURE'}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: result.RolledAt,
        isSystem: true,
        skill: result.Skill,
        skillDC: result.DC,
        skillResult: result.Success ? 'success' : 'failure',
      }]);
    });

    on('AttackResult', (result: any) => {
      setMessages(prev => [...prev, {
        id: `attack-${Date.now()}`,
        type: 'attack',
        content: `${result.Weapon} vs ${result.Target}: ${result.Hit ? `HIT! ${result.DamageTotal} damage` : 'MISS'}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: result.RolledAt,
        isSystem: true,
        attackWeapon: result.Weapon,
        attackTarget: result.Target,
        attackHit: result.Hit,
        attackDamage: result.DamageTotal,
      }]);
    });

    on('Error', (err: any) => {
      setErrorState(err.message);
    });

    on('AgentCallStarted', (call: any) => {
      setMessages(prev => [...prev, {
        id: `agent-start-${call.Id}`,
        type: 'agentCall',
        content: `${getAgentLabel(call.FromAgent)} → ${getAgentLabel(call.ToAgent)}: ${getActionLabel(call.Action)}`,
        senderName: 'Agent Framework',
        senderRole: 'System',
        timestamp: call.CreatedAt,
        isSystem: true,
        agentFrom: getAgentLabel(call.FromAgent),
        agentAction: getActionLabel(call.Action),
        agentStatus: 'Running',
      }]);
    });

    on('AgentCallCompleted', (call: any) => {
      // Update the running agent call message
      setMessages(prev => prev.map(m =>
        m.id === `agent-start-${call.Id}`
          ? { ...m, agentStatus: call.Status, content: `${m.content} → ${call.Status}` }
          : m
      ));
      // Also add a completion message
      setMessages(prev => [...prev, {
        id: `agent-end-${call.Id}`,
        type: 'agentResponse',
        content: call.OutputMessage || call.Output || call.Status,
        senderName: getAgentLabel(call.ToAgent),
        senderRole: 'System',
        timestamp: call.CompletedAt || call.CreatedAt,
        isSystem: true,
        agentFrom: getAgentLabel(call.ToAgent),
        agentAction: getActionLabel(call.Action),
        agentStatus: call.Status,
      }]);
    });

    // ==================== Tool Call Events ====================

    on('ToolCallNotification', (event: any) => {
      setMessages(prev => [...prev, {
        id: `tool-notif-${event.toolCallId}`,
        type: 'agentCall',
        content: `🔧 ${event.toolName}: ${event.outputMessage}`,
        senderName: '🤖 AI-GM',
        senderRole: 'GM',
        timestamp: event.timestamp,
        isSystem: true,
        agentFrom: '🤖 AI-GM',
        agentAction: event.toolName,
        agentStatus: event.requiresConfirmation ? 'Waiting' : 'Running',
      }]);
      refetchToolCalls();
    });

    on('ToolCallConfirmed', async (event: any) => {
      if (event.approved) {
        // Tool was approved — add result as narrative message
        if (event.outputMessage) {
          setMessages(prev => [...prev, {
            id: `tool-confirm-${event.toolCallId}`,
            type: 'agentResponse',
            content: `✅ ${event.outputMessage}`,
            senderName: '🤖 AI-GM',
            senderRole: 'GM',
            timestamp: new Date().toISOString(),
            isSystem: true,
            agentFrom: '🤖 AI-GM',
            agentAction: event.toolName,
            agentStatus: 'Completed',
          }]);
        }
        // If this was a narration, also post narrative to chat
        if (event.toolName === 'narrate' && event.result) {
          try {
            const parsed = JSON.parse(event.result as string);
            if (parsed.context) {
              setMessages(prev => [...prev, {
                id: `narrative-${Date.now()}`,
                type: 'inGamePublic',
                content: parsed.context,
                senderName: '🤖 AI-GM',
                senderRole: 'GM',
                timestamp: new Date().toISOString(),
              }]);
            }
          } catch { /* ignore parse errors */ }
        }
      } else {
        setMessages(prev => [...prev, {
          id: `tool-denied-${event.toolCallId}`,
          type: 'system',
          content: `❌ ${event.toolName} denied by GM`,
          senderName: '⚙️ System',
          senderRole: 'System',
          timestamp: new Date().toISOString(),
          isSystem: true,
        }]);
      }
      refetchToolCalls();
    });

    on('PlayerRollRequested', (event: any) => {
      setRollSkill(event.skill || '');
      setRollFormula(event.formula || '1d20');
      setRollDC(event.dc || 15);
      setRollContext(event.context || '');
      setRollOptional(event.optional === true);
      
      setShowRollDialog(true);
    });

    on('PlayerRollConfirmed', (event: any) => {
      setMessages(prev => [...prev, {
        id: `roll-confirm-${event.toolCallId}`,
        type: 'skillCheck',
        content: `${event.playerName} confirmed roll: ${event.formula} vs DC ${event.dc} (${event.skill})`,
        senderName: event.playerName,
        senderRole: 'Player',
        timestamp: new Date().toISOString(),
        isSystem: true,
        skill: event.skill,
        skillDC: event.dc,
        skillResult: 'pending',
      }]);
    });

    on('PlayerRollDeclined', (event: any) => {
      setMessages(prev => [...prev, {
        id: `roll-declined-${event.toolCallId}`,
        type: 'system',
        content: `${event.playerName} declined to roll ${event.skill}`,
        senderName: '⚙️ System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
      }]);
      refetchToolCalls();
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

  // ==================== Unified Send Handler ====================

  const handleUnifiedSend = async () => {
    if (!selectedSession) return;

    // Handle whisper input (GM whispering to player)
    if (inputTarget === 'player' && whisperTargetPlayer) {
      if (inputType === 'inGame') {
        await invoke('SendInGameWhisperToPlayer', whisperTargetPlayer, whisperInput);
      } else {
        await invoke('SendOOCWhisperToPlayer', whisperTargetPlayer, whisperInput);
      }
      setWhisperInput('');
      setWhisperTargetPlayer(null);
      return;
    }

    // Handle regular message send
    if (inputType === 'inGame') {
      if (inputTarget === 'gm') {
        // In-game whisper to GM
        await invoke('SendInGameWhisper', selectedSession, whisperInput);
        setWhisperInput('');
        return;
      }
      // In-game public
      await invoke('SendMessage', selectedSession, whisperInput);
      setWhisperInput('');
    } else {
      // OOC public
      await invoke('SendOOCMessage', selectedSession, whisperInput);
      setWhisperInput('');
    }
  };

  const handleRefreshAgentCalls = async () => {
    if (!id) return;
    try {
      const calls = await invoke('GetAgentCallHistory', id);
      if (calls) {
        // Add agent calls as messages
        const agentMsgs: UnifiedMessage[] = calls.map((call: any) => ({
          id: `agent-${call.Id}`,
          type: call.Status === 'Running' ? 'agentCall' : 'agentResponse',
          content: `${getAgentLabel(call.FromAgent)} → ${getAgentLabel(call.ToAgent)}: ${getActionLabel(call.Action)} → ${call.Status}`,
          senderName: 'Agent Framework',
          senderRole: 'System',
          timestamp: call.CreatedAt,
          isSystem: true,
          agentFrom: getAgentLabel(call.FromAgent),
          agentAction: getActionLabel(call.Action),
          agentStatus: call.Status,
        }));
        setMessages(prev => [...prev, ...agentMsgs]);
      }
    } catch (e) {
      // ignore
    }
  };

  const handleDiceRoll = async () => {
    if (!selectedSession) return;
    try {
      const result = await invoke('RollDice', selectedSession, diceFormula, user?.id);
      setMessages(prev => [...prev, {
        id: `dice-${Date.now()}`,
        type: 'dice',
        content: `${result.Formula} → ${result.Total}`,
        senderName: 'Player',
        senderRole: 'Player',
        timestamp: result.Timestamp,
        isSystem: true,
        diceFormula: result.Formula,
        diceTotal: result.Total,
        diceRolls: result.Rolls,
      }]);
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

  const handleCreateCharacter = async (characterData: any) => {
    if (!id || !user?.id) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      // Find the current user's player ID
      const playersList = await api.getPlayers(id);
      const myPlayer = playersList.find((p: any) => p.userEmail === user.email || p.userName === user.displayName);
      if (!myPlayer) {
        setErrorState('Could not find your player record. Are you joined to this game?');
        return;
      }

      await invoke('CreateCharacter', id, myPlayer.id, JSON.stringify(characterData));
      setSuccessState(`Character '${characterData.name}' created!`);
      setTimeout(() => setSuccessState(null), 2000);
      setShowCharacterWizard(false);
      // Refetch characters
      refetchPlayers();
    } catch (e: any) {
      setErrorState(e.message || 'Failed to create character');
    }
  };

  // ==================== Tool Call Handlers ====================

  const handleConfirmToolCall = async (toolCallId: string, approved: boolean) => {
    const result = await confirmToolCall(toolCallId, approved);
    if (result && approved && result.outputMessage) {
      setMessages(prev => [...prev, {
        id: `tool-result-${toolCallId}`,
        type: 'agentResponse',
        content: `✅ ${result.outputMessage}`,
        senderName: '🤖 AI-GM',
        senderRole: 'GM',
        timestamp: new Date().toISOString(),
        isSystem: true,
        agentFrom: '🤖 AI-GM',
        agentAction: 'Tool',
        agentStatus: 'Completed',
      }]);
    }
  };

  const handleDismissToolCall = async (toolCallId: string) => {
    await confirmToolCall(toolCallId, false);
  };

  const handleConfirmPlayerRoll = async (toolCallId: string) => {
    const result = await confirmPlayerRoll(toolCallId);
    if (result) {
      setRollSkill(result.skill);
      setRollFormula(result.formula);
      setRollDC(result.dc);
      setRollContext(result.context);
      setRollOptional(result.optional);
      
      setShowRollDialog(true);
    }
  };

  const handleDeclinePlayerRoll = async (toolCallId: string) => {
    await declinePlayerRoll(toolCallId);
  };

  const handlePlayerRoll = async (formula: string) => {
    if (!selectedSession) return;
    try {
      const result = await invoke('RollDice', selectedSession, formula, user?.id);
      setMessages(prev => [...prev, {
        id: `dice-${Date.now()}`,
        type: 'dice',
        content: `${result.Formula} → ${result.Total}`,
        senderName: 'Player',
        senderRole: 'Player',
        timestamp: result.Timestamp,
        isSystem: true,
        diceFormula: result.Formula,
        diceTotal: result.Total,
        diceRolls: result.Rolls,
      }]);
      setShowDiceHistory(true);
      setShowRollDialog(false);
    } catch (e: any) {
      setErrorState(e.message);
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

  const activeSession = sessions?.find((s: any) => !s.endedAt);
  const isCreator = players.some((p: any) => p.role === 'Creator');

  // Tabs: Chat is always first, then other panels
  const tabs = [
    { label: 'Chat', icon: <ChatBubbleIcon /> },
    { label: 'Combat', icon: <CombatIcon />, count: activeCombats.length > 0 ? activeCombats.length : undefined },
    { label: 'Players', icon: <PeopleIcon />, count: players.length },
    { label: 'Characters', icon: <SheetIcon />, count: characters.length },
    { label: 'Actions', icon: <ActionIcon /> },
    { label: 'Agent Calls', icon: <DiceIcon /> },
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
            {!isConnected && <Chip label="Disconnected" size="small" color="error" />}
            {activeSession && <Chip label={`Session: ${activeSession.title}`} size="small" variant="outlined" />}
          </Box>
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

      {/* ==================== Tool Call Banner ==================== */}
      <ToolCallBanner
        pendingCalls={pendingCalls}
        onConfirm={handleConfirmToolCall}
        onRoll={handleConfirmPlayerRoll}
        onDecline={handleDeclinePlayerRoll}
        onDismiss={handleDismissToolCall}
        isCreator={isCreator}
      />

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
        <Box sx={{ flex: 1 }}>
          {activeTab === 0 && (
            <UnifiedChatPanel
              messages={messages}
              inputType={inputType}
              setInputType={setInputType}
              inputTarget={inputTarget}
              setInputTarget={setInputTarget}
              whisperTargetPlayer={whisperTargetPlayer}
              setWhisperTargetPlayer={setWhisperTargetPlayer}
              whisperInput={whisperInput}
              setWhisperInput={setWhisperInput}
              onSend={handleUnifiedSend}
              onDiceRoll={() => setOpenDiceDialog(true)}
              onSkillCheck={handleSkillCheck}
              showDiceHistory={showDiceHistory}
              setShowDiceHistory={setShowDiceHistory}
              messagesEndRef={messagesEndRef}
              players={players}
              isConnected={isConnected}
              activeSession={activeSession}
              isCreator={players.some((p: any) => p.role === 'Creator')}
            />
          )}

          {activeTab === 1 && (
            <CombatTab gameId={id || ''} />
          )}

          {activeTab === 2 && (
            <PlayersTab players={players} currentUserId={user?.id} />
          )}

          {activeTab === 3 && (
            <CharactersTab characters={characters} currentUserName={user?.displayName} onCreateCharacter={() => setShowCharacterWizard(true)} />
          )}

          {activeTab === 4 && (
            <ActionsTab onSkillCheck={handleSkillCheck} onAttack={handleAttack} onDiceRoll={() => setOpenDiceDialog(true)} />
          )}

          {activeTab === 5 && (
            <AgentCallsTab
              calls={[]}
              onRefresh={handleRefreshAgentCalls}
            />
          )}

          {activeTab === 6 && (
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

      {/* ==================== Player Roll Dialog ==================== */}
      <PlayerRollDialog
        open={showRollDialog}
        onClose={() => setShowRollDialog(false)}
        skill={rollSkill}
        formula={rollFormula}
        dc={rollDC}
        context={rollContext}
        optional={rollOptional}
        onRoll={handlePlayerRoll}
      />

      {/* Character Creation Dialog */}
      <CharacterCreateWizard
        open={showCharacterWizard}
        onClose={() => setShowCharacterWizard(false)}
        onFinish={handleCreateCharacter}
      />
    </Container>
  );
}

// ==================== Helper Functions ====================

function getAgentLabel(agent: number): string {
  switch (agent) {
    case AgentType.Creator: return '🎬 Creator';
    case AgentType.GM: return '🤖 AI-GM';
    case AgentType.LLM: return '🤖 LLM';
    case AgentType.Dice: return '🎲 Dice';
    case AgentType.RAG: return '📚 RAG';
    case AgentType.NPC: return '🧙 NPC';
    case AgentType.Player: return '👤 Player';
    case AgentType.System: return '⚙️ System';
    default: return '❓ Unknown';
  }
}

function getActionLabel(action: number): string {
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
    case AgentAction.Nudge: return 'Nudge';
    case AgentAction.CreateCharacter: return 'CreateChar';
    default: return 'Unknown';
  }
}

// ==================== Unified Chat Panel ====================

interface UnifiedChatPanelProps {
  messages: UnifiedMessage[];
  inputType: MessageInputType;
  setInputType: (t: MessageInputType) => void;
  inputTarget: MessageTarget;
  setInputTarget: (t: MessageTarget) => void;
  whisperTargetPlayer: string | null;
  setWhisperTargetPlayer: (t: string | null) => void;
  whisperInput: string;
  setWhisperInput: (t: string) => void;
  onSend: () => Promise<void>;
  onDiceRoll: () => void;
  onSkillCheck: (skill: string, dc: number) => void;
  showDiceHistory: boolean;
  setShowDiceHistory: (v: boolean) => void;
  messagesEndRef: React.RefObject<HTMLDivElement | null>;
  players: any[];
  isConnected: boolean;
  activeSession: any;
  isCreator: boolean;
}

function UnifiedChatPanel({
  messages, inputType, setInputType, inputTarget, setInputTarget,
  whisperTargetPlayer, setWhisperTargetPlayer, whisperInput, setWhisperInput,
  onSend, onDiceRoll, onSkillCheck, showDiceHistory, setShowDiceHistory: _setShowDiceHistory,
  messagesEndRef, players, isConnected: _isConnected, activeSession, isCreator
}: UnifiedChatPanelProps) {
  const [quickSkill, setQuickSkill] = useState('Perception');
  const [quickDC, setQuickDC] = useState(15);

  const handleSend = () => {
    onSend();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <Paper sx={{ height: '75vh', display: 'flex', flexDirection: 'column' }}>
      {/* ===== Message Type & Receiver Selector ===== */}
      <Box sx={{
        p: 1.5,
        borderBottom: 1,
        borderColor: 'divider',
        display: 'flex',
        gap: 1.5,
        alignItems: 'center',
        flexWrap: 'wrap',
        bgcolor: 'background.paper'
      }}>
        {/* Message Type Toggle */}
        <Box sx={{ display: 'flex', bgcolor: 'background.default', borderRadius: 1, overflow: 'hidden' }}>
          <Button
            size="small"
            onClick={() => setInputType('inGame')}
            sx={{
              bgcolor: inputType === 'inGame' ? 'success.lighter' : 'transparent',
              color: inputType === 'inGame' ? 'success.dark' : 'text.secondary',
              minWidth: 90,
              px: 2,
              fontSize: 12,
              fontWeight: inputType === 'inGame' ? 600 : 400,
              '&:hover': { bgcolor: inputType === 'inGame' ? 'success.lighter' : 'action.hover' }
            }}
          >
            🎮 In-Game
          </Button>
          <Button
            size="small"
            onClick={() => setInputType('ooc')}
            sx={{
              bgcolor: inputType === 'ooc' ? 'info.lighter' : 'transparent',
              color: inputType === 'ooc' ? 'info.dark' : 'text.secondary',
              minWidth: 90,
              px: 2,
              fontSize: 12,
              fontWeight: inputType === 'ooc' ? 600 : 400,
              '&:hover': { bgcolor: inputType === 'ooc' ? 'info.lighter' : 'action.hover' }
            }}
          >
            📢 OOC
          </Button>
        </Box>

        {/* Receiver Selector */}
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel sx={{ fontSize: 11 }}>Receiver</InputLabel>
          <Select
            value={inputTarget}
            label="Receiver"
            onChange={e => setInputTarget(e.target.value as MessageTarget)}
            sx={{ fontSize: 12, height: 32 }}
          >
            <MenuItem value="all">🌐 All (Public)</MenuItem>
            <MenuItem value="gm">🤫 To GM (Whisper)</MenuItem>
            {isCreator && (
              <MenuItem value="player">📩 To Player...</MenuItem>
            )}
          </Select>
        </FormControl>

        {/* Quick Actions */}
        <Chip label="🎲 Dice" size="small" clickable onClick={onDiceRoll} sx={{ fontSize: 11 }} />
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <TextField
            size="small"
            value={quickSkill}
            onChange={e => setQuickSkill(e.target.value)}
            sx={{ width: 100, '& .MuiInputBase-root': { height: 32, fontSize: 12 } }}
            placeholder="Skill"
          />
          <TextField
            size="small"
            type="number"
            value={quickDC}
            onChange={e => setQuickDC(parseInt(e.target.value) || 0)}
            sx={{ width: 60, '& .MuiInputBase-root': { height: 32, fontSize: 12 } }}
            placeholder="DC"
          />
          <Chip
            label="📋 Check"
            size="small"
            clickable
            onClick={() => onSkillCheck(quickSkill, quickDC)}
            sx={{ fontSize: 11 }}
          />
        </Box>

        {activeSession && (
          <Chip label={`📋 ${activeSession.title}`} size="small" variant="outlined" sx={{ fontSize: 11 }} />
        )}
      </Box>

      {/* Player Whisper Target Selector (GM only) */}
      {isCreator && inputTarget === 'player' && (
        <Box sx={{
          p: 1,
          borderBottom: 1,
          borderColor: 'divider',
          display: 'flex',
          gap: 1,
          alignItems: 'center',
          flexWrap: 'wrap',
          bgcolor: 'background.default'
        }}>
          <Typography variant="caption" color="text.secondary">
            {inputType === 'inGame' ? 'In-game whisper to:' : 'OOC whisper to:'}
          </Typography>
          {players.filter((p: any) => p.status === 'Active').map((p: any) => (
            <Chip
              key={p.id}
              label={p.characterName}
              size="small"
              clickable
              onClick={() => setWhisperTargetPlayer(p.id)}
              sx={{ m: 0.25 }}
            />
          ))}
        </Box>
      )}

      {/* Dice History */}
      <Collapse in={showDiceHistory}>
        <Box sx={{
          p: 1,
          maxHeight: 120,
          overflow: 'auto',
          bgcolor: 'background.default',
          borderBottom: 1,
          borderColor: 'divider'
        }}>
          <Typography variant="caption" color="text.secondary">Recent Rolls:</Typography>
          {messages
            .filter(m => m.type === 'dice')
            .slice(-10)
            .reverse()
            .map((m, i) => (
              <Typography key={i} variant="caption" sx={{ display: 'block' }}>
                {m.diceFormula}: {m.diceTotal} [{m.diceRolls?.join(',')}]{m.diceRolls ? '' : ''} — {m.senderName}
              </Typography>
            ))}
        </Box>
      </Collapse>

      {/* ===== Unified Messages ===== */}
      <Box sx={{ flex: 1, overflow: 'auto', p: 1.5 }}>
        {messages.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 8 }}>
            No messages yet. Start the conversation!
          </Typography>
        ) : (
          messages.map((msg, i) => (
            <MessageBubble key={i} msg={msg} />
          ))
        )}
        <div ref={messagesEndRef} />
      </Box>

      {/* ===== Input Area ===== */}
      <Box sx={{
        p: 1.5,
        borderTop: 1,
        borderColor: 'divider',
        bgcolor: 'background.paper'
      }}>
        {/* Player whisper confirmation */}
        {whisperTargetPlayer && (
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
            <Typography variant="caption" color="text.secondary">
              📩 Whispering to player: {players.find(p => p.id === whisperTargetPlayer)?.characterName}
            </Typography>
            <Button size="small" onClick={() => setWhisperTargetPlayer(null)}>Clear</Button>
          </Box>
        )}

        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            fullWidth
            size="small"
            placeholder={
              inputTarget === 'gm'
                ? `${inputType === 'inGame' ? 'In-game' : 'OOC'} whisper to GM...`
                : inputTarget === 'player'
                  ? 'Type your whisper...'
                  : inputType === 'inGame'
                    ? 'Speak in-character (visible to all)...'
                    : 'Speak out-of-character (never influences narrative)...'
            }
            value={whisperInput}
            onChange={e => setWhisperInput(e.target.value)}
            onKeyDown={handleKeyDown}
            multiline
            maxRows={4}
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={handleSend} size="small" disabled={!whisperInput.trim()}>
                    <SendIcon />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
          <Button
            variant="contained"
            onClick={handleSend}
            disabled={!whisperInput.trim()}
            sx={{ minWidth: 80 }}
          >
            Send
          </Button>
        </Box>
      </Box>
    </Paper>
  );
}

// ==================== Individual Message Bubble ====================

function MessageBubble({ msg }: { msg: UnifiedMessage }) {
  const style = MESSAGE_STYLES[msg.type];
  const isWhisper = msg.isWhisper || msg.type === 'inGameWhisper' || msg.type === 'oocWhisper';
  const isSystem = msg.isSystem || msg.type === 'dice' || msg.type === 'skillCheck' || msg.type === 'attack';

  return (
    <Box sx={{
      mb: 1,
      p: 1.5,
      borderRadius: 2,
      bgcolor: style.bg,
      borderLeft: `3px solid ${style.border}`,
    }}>
      {/* Header: Type chip + sender + time */}
      <Box sx={{ display: 'flex', gap: 0.75, alignItems: 'center', flexWrap: 'wrap', mb: 0.5 }}>
        <Chip
          label={`${style.chipIcon} ${style.chipLabel}`}
          size="small"
          color={style.chipColor}
          sx={{ height: 18, fontSize: 10, fontWeight: 600 }}
        />
        {isWhisper && (
          <Chip
            label={`🤫 → ${msg.whisperTo || 'Unknown'}`}
            size="small"
            color="warning"
            sx={{ height: 18, fontSize: 9 }}
          />
        )}
        <Typography variant="caption" sx={{ fontWeight: 600, color: 'text.primary' }}>
          {msg.senderName}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {new Date(msg.timestamp).toLocaleTimeString()}
        </Typography>
      </Box>

      {/* Content */}
      <Typography variant="body2" sx={{
        color: isSystem ? 'text.secondary' : 'text.primary',
        fontStyle: isWhisper ? 'italic' : 'normal',
        wordBreak: 'break-word',
      }}>
        {msg.content}
      </Typography>

      {/* Extra info for dice/skill/attack */}
      {msg.type === 'dice' && msg.diceRolls && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Rolls: [{msg.diceRolls.join(', ')}]
        </Typography>
      )}
      {msg.type === 'skillCheck' && msg.skillResult && (
        <Chip
          label={msg.skillResult === 'success' ? '✓ Success' : '✗ Failure'}
          size="small"
          color={msg.skillResult === 'success' ? 'success' : 'error'}
          sx={{ mt: 0.5, height: 20, fontSize: 10 }}
        />
      )}
      {msg.type === 'attack' && (
        <Box sx={{ mt: 0.5 }}>
          <Chip
            label={msg.attackHit ? '✓ Hit' : '✗ Miss'}
            size="small"
            color={msg.attackHit ? 'success' : 'error'}
            sx={{ height: 20, fontSize: 10, mr: 0.5 }}
          />
          {msg.attackDamage && (
            <Chip
              label={`${msg.attackDamage} damage`}
              size="small"
              color="warning"
              sx={{ height: 20, fontSize: 10 }}
            />
          )}
        </Box>
      )}
      {msg.type === 'agentCall' && msg.agentStatus && (
        <Chip
          label={msg.agentStatus}
          size="small"
          color={msg.agentStatus === 'Completed' ? 'success' : msg.agentStatus === 'Running' ? 'warning' : 'error'}
          sx={{ mt: 0.5, height: 20, fontSize: 10 }}
        />
      )}
    </Box>
  );
}

// ==================== Sub-Components ====================

function PlayersTab({ players, currentUserId }: { players: any[]; currentUserId?: string }) {
  const filteredPlayers = players.filter(p => p.id !== currentUserId);
  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>Other Players ({filteredPlayers.length})</Typography>
      <List>
        {filteredPlayers.map((p: any) => (
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

function CharactersTab({ characters, currentUserName, onCreateCharacter }: { characters: any[]; currentUserName?: string; onCreateCharacter?: () => void }) {
  const navigate = useNavigate();
  const myCharacter = characters.find(c => c.playerName === currentUserName);
  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>My Character</Typography>
      {!currentUserName ? (
        <Typography color="text.secondary">Unable to identify your character.</Typography>
      ) : myCharacter ? (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
              <Typography variant="h6">{myCharacter.name}</Typography>
              <Chip label={`${myCharacter.class} Lv.${myCharacter.level}`} size="small" />
            </Box>
            <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
              <Chip label={`HP: ${myCharacter.currentHP}/${myCharacter.maxHP}`} size="small" color={myCharacter.currentHP < myCharacter.maxHP * 0.3 ? 'error' : 'default'} />
            </Box>
            <Typography variant="caption" color="text.secondary">
              Last updated: {new Date(myCharacter.updatedAt).toLocaleString()}
            </Typography>
            <Box sx={{ mt: 1, display: 'flex', gap: 1 }}>
              <Button size="small" variant="outlined" startIcon={<SheetIcon />}
                onClick={() => navigate(`/character/${myCharacter.id}`)}>
                View Sheet
              </Button>
            </Box>
          </Paper>
        </Box>
      ) : (
        <>
          <Typography color="text.secondary" sx={{ mb: 2 }}>No character found. Create one below.</Typography>
          <Button variant="contained" startIcon={<PeopleIcon />} onClick={onCreateCharacter}>Create Character</Button>
        </>
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
      case AgentType.Creator: return '🎬 Creator';
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
      case AgentAction.Nudge: return 'Nudge';
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

                    <TableCell sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {call.outputMessage || call.output || '-'}
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
