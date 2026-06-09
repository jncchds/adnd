import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../api/authHook';
import { useGame, useSessions, usePlayers, useGMStatus, useSway } from '../api/gameHooks';
import { useGameHub } from '../api/hubHook';
import { useToolCalls } from '../api/toolCallsHook';
import { useMessagesInfiniteScroll, UnifiedMessage, UnifiedMessageType } from '../api/gameToolsHook';
import { api } from '../api/client';
import { WhisperType, AgentType, AgentAction } from '../types';
import CombatTab from './CombatTab';
import CharacterCreateWizard from './CharacterCreateWizard';
import ToolCallBanner from '../components/ToolCallBanner';
import PlayerRollDialog from '../components/PlayerRollDialog';
import {
  Box, Typography, Paper, TextField, Button,
  List, ListItem, ListItemText, Chip,
  Dialog, DialogTitle, DialogContent, DialogActions,
  IconButton, Divider, Alert, AlertTitle, Collapse,
  InputAdornment, MenuItem, Select, FormControl,

  InputLabel, useMediaQuery, useTheme
} from '@mui/material';
import MarkdownRenderer from '../components/MarkdownRenderer';
import { Send as SendIcon, SportsEsports as DiceIcon,
  Replay as ReplayIcon, ExitToApp as LeaveIcon, People as PeopleIcon } from '@mui/icons-material';

// ==================== Markdown Support ====================

/** Message types that render with markdown */
const MARKDOWN_TYPES = new Set<UnifiedMessageType>([
  'inGamePublic', 'inGameWhisper', 'oocPublic', 'oocWhisper',
  'narration', 'gm', 'plotReview', 'consistencyCheck', 'suggestion'
]);

/** Check if content contains markdown syntax */
function hasMarkdownSyntax(content: string): boolean {
  // Check for common markdown patterns
  return /\*\*|\*_|~~|`{1,3}|\[.+\]\(|^#{1,6}\s|^[\-\*\+]\s|^[0-9]+\.\s|^>\s|^---|^- \[|^- \[x\]/m.test(content);
}

// ==================== Message Color Config ====================

const MESSAGE_STYLES: Record<UnifiedMessageType, {
  bg: string;
  border: string;
  chipColor: 'primary' | 'info' | 'warning' | 'success' | 'error' | 'default';
  chipLabel: string;
  chipIcon: string;
}> = {
  // Chat messages
  inGamePublic:    { bg: 'rgba(103, 194, 58, 0.06)',   border: 'rgba(103, 194, 58, 0.25)',  chipColor: 'success', chipLabel: 'In-Game', chipIcon: '🎮' },
  inGameWhisper:   { bg: 'rgba(255, 193, 7, 0.08)',    border: 'rgba(255, 193, 7, 0.35)',  chipColor: 'warning', chipLabel: 'Whisper', chipIcon: '🤫' },
  oocPublic:       { bg: 'rgba(33, 150, 243, 0.06)',   border: 'rgba(33, 150, 243, 0.25)',  chipColor: 'info',    chipLabel: 'OOC', chipIcon: '📢' },
  oocWhisper:      { bg: 'rgba(156, 39, 176, 0.06)',   border: 'rgba(156, 39, 176, 0.25)',  chipColor: 'info', chipLabel: 'OOC Whisper', chipIcon: '🤫' },
  // Game actions
  dice:            { bg: 'rgba(255, 152, 0, 0.05)',    border: 'rgba(255, 152, 0, 0.20)',  chipColor: 'default', chipLabel: '🎲 Dice', chipIcon: '🎲' },
  skillCheck:      { bg: 'rgba(156, 39, 176, 0.05)',   border: 'rgba(156, 39, 176, 0.20)',  chipColor: 'info', chipLabel: '📋 Check', chipIcon: '📋' },
  attack:          { bg: 'rgba(244, 67, 54, 0.05)',    border: 'rgba(244, 67, 54, 0.20)',  chipColor: 'error', chipLabel: '⚔️ Attack', chipIcon: '⚔️' },
  spellCast:       { bg: 'rgba(156, 39, 176, 0.08)',   border: 'rgba(156, 39, 176, 0.30)',  chipColor: 'info', chipLabel: '✨ Spell', chipIcon: '✨' },
  // Combat
  combatStart:     { bg: 'rgba(244, 67, 54, 0.08)',    border: 'rgba(244, 67, 54, 0.30)',  chipColor: 'error', chipLabel: '⚔️ Combat', chipIcon: '⚔️' },
  combatEnd:       { bg: 'rgba(76, 175, 80, 0.08)',    border: 'rgba(76, 175, 80, 0.30)',  chipColor: 'success', chipLabel: '⚔️ End', chipIcon: '✅' },
  combatPause:     { bg: 'rgba(255, 193, 7, 0.06)',    border: 'rgba(255, 193, 7, 0.25)',  chipColor: 'warning', chipLabel: '⏸️ Pause', chipIcon: '⏸️' },
  combatResume:    { bg: 'rgba(76, 175, 80, 0.06)',    border: 'rgba(76, 175, 80, 0.25)',  chipColor: 'success', chipLabel: '▶️ Resume', chipIcon: '▶️' },
  initiative:      { bg: 'rgba(255, 152, 0, 0.05)',    border: 'rgba(255, 152, 0, 0.20)',  chipColor: 'default', chipLabel: '🎲 Init', chipIcon: '🎲' },
  initiativeComplete: { bg: 'rgba(255, 152, 0, 0.05)', border: 'rgba(255, 152, 0, 0.20)',  chipColor: 'default', chipLabel: '📊 Init Order', chipIcon: '📊' },
  turnAdvanced:    { bg: 'rgba(33, 150, 243, 0.05)',   border: 'rgba(33, 150, 243, 0.20)',  chipColor: 'info', chipLabel: '⏩ Turn', chipIcon: '⏩' },
  turnRetreated:   { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '↩️ Retreat', chipIcon: '↩️' },
  turnSet:         { bg: 'rgba(33, 150, 243, 0.05)',   border: 'rgba(33, 150, 243, 0.20)',  chipColor: 'info', chipLabel: '🎯 Set Turn', chipIcon: '🎯' },
  damage:          { bg: 'rgba(244, 67, 54, 0.08)',    border: 'rgba(244, 67, 54, 0.30)',  chipColor: 'error', chipLabel: '💥 Damage', chipIcon: '💥' },
  heal:            { bg: 'rgba(76, 175, 80, 0.08)',    border: 'rgba(76, 175, 80, 0.30)',  chipColor: 'success', chipLabel: '💚 Heal', chipIcon: '💚' },
  deathSave:       { bg: 'rgba(244, 67, 54, 0.06)',    border: 'rgba(244, 67, 54, 0.25)',  chipColor: 'error', chipLabel: '💀 Death Save', chipIcon: '💀' },
  conditionApplied: { bg: 'rgba(244, 67, 54, 0.06)',   border: 'rgba(244, 67, 54, 0.25)',  chipColor: 'error', chipLabel: '🔴 Condition', chipIcon: '🔴' },
  conditionRemoved: { bg: 'rgba(76, 175, 80, 0.06)',   border: 'rgba(76, 175, 80, 0.25)',  chipColor: 'success', chipLabel: '🟢 Condition', chipIcon: '🟢' },
  xpGranted:       { bg: 'rgba(255, 193, 7, 0.06)',    border: 'rgba(255, 193, 7, 0.25)',  chipColor: 'warning', chipLabel: '⭐ XP', chipIcon: '⭐' },
  levelUp:         { bg: 'rgba(255, 193, 7, 0.08)',    border: 'rgba(255, 193, 7, 0.30)',  chipColor: 'warning', chipLabel: '🆙 Level Up', chipIcon: '🆙' },
  sanLoss:         { bg: 'rgba(103, 194, 58, 0.06)',   border: 'rgba(103, 194, 58, 0.25)',  chipColor: 'success', chipLabel: '🧠 SAN Loss', chipIcon: '🧠' },
  sanRecovery:     { bg: 'rgba(33, 150, 243, 0.06)',   border: 'rgba(33, 150, 243, 0.25)',  chipColor: 'info', chipLabel: '🧠 SAN Rec', chipIcon: '🧠' },
  sanCheck:        { bg: 'rgba(156, 39, 176, 0.06)',   border: 'rgba(156, 39, 176, 0.25)',  chipColor: 'info', chipLabel: '🧠 SAN Check', chipIcon: '🧠' },
  // Action economy
  actionSpent:     { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '🎯 Action', chipIcon: '🎯' },
  bonusActionSpent: { bg: 'rgba(158, 158, 158, 0.05)', border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '⚡ Bonus', chipIcon: '⚡' },
  reactionSpent:   { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '🔄 Reaction', chipIcon: '🔄' },
  movementSpent:   { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '🚶 Move', chipIcon: '🚶' },
  actionsRefreshed: { bg: 'rgba(76, 175, 80, 0.06)',   border: 'rgba(76, 175, 80, 0.25)',  chipColor: 'success', chipLabel: '🔄 Refresh', chipIcon: '🔄' },
  // Combat state
  participantAdded: { bg: 'rgba(103, 194, 58, 0.06)',  border: 'rgba(103, 194, 58, 0.25)',  chipColor: 'success', chipLabel: '➕ Join', chipIcon: '➕' },
  participantRemoved: { bg: 'rgba(158, 158, 158, 0.05)', border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '➖ Leave', chipIcon: '➖' },
  gridSet:         { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '🗺️ Grid', chipIcon: '🗺️' },
  positionSet:     { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '📍 Position', chipIcon: '📍' },
  combatMove:      { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '🚶 Move', chipIcon: '🚶' },
  itemAdded:       { bg: 'rgba(33, 150, 243, 0.05)',   border: 'rgba(33, 150, 243, 0.20)',  chipColor: 'info', chipLabel: '📦 Item', chipIcon: '📦' },
  itemRemoved:     { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '🗑️ Item', chipIcon: '🗑️' },
  itemEquipped:    { bg: 'rgba(76, 175, 80, 0.05)',    border: 'rgba(76, 175, 80, 0.20)',  chipColor: 'success', chipLabel: '⚔️ Equip', chipIcon: '⚔️' },
  itemUnequipped:  { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '📦 Unequip', chipIcon: '📦' },
  // Player lifecycle
  playerJoined:    { bg: 'rgba(103, 194, 58, 0.06)',   border: 'rgba(103, 194, 58, 0.25)',  chipColor: 'success', chipLabel: '👤 Joined', chipIcon: '👤' },
  playerLeft:      { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '👤 Left', chipIcon: '👤' },
  playerDisconnected: { bg: 'rgba(255, 152, 0, 0.06)', border: 'rgba(255, 152, 0, 0.25)',  chipColor: 'warning', chipLabel: '⚠️ Disconnected', chipIcon: '⚠️' },
  playerReconnected: { bg: 'rgba(103, 194, 58, 0.06)', border: 'rgba(103, 194, 58, 0.25)',  chipColor: 'success', chipLabel: '✅ Reconnected', chipIcon: '✅' },
  playerRoleChanged: { bg: 'rgba(33, 150, 243, 0.06)', border: 'rgba(33, 150, 243, 0.25)',  chipColor: 'info', chipLabel: '🔄 Role', chipIcon: '🔄' },
  // Character lifecycle
  characterCreated: { bg: 'rgba(103, 194, 58, 0.06)',  border: 'rgba(103, 194, 58, 0.25)',  chipColor: 'success', chipLabel: '📝 Character', chipIcon: '📝' },
  characterUpdated: { bg: 'rgba(33, 150, 243, 0.06)',  border: 'rgba(33, 150, 243, 0.25)',  chipColor: 'info', chipLabel: '📝 Updated', chipIcon: '📝' },
  // Session/Game lifecycle
  sessionCreated:  { bg: 'rgba(33, 150, 243, 0.06)',   border: 'rgba(33, 150, 243, 0.25)',  chipColor: 'info', chipLabel: '📋 Session', chipIcon: '📋' },
  sessionClosed:   { bg: 'rgba(158, 158, 158, 0.06)',  border: 'rgba(158, 158, 158, 0.25)', chipColor: 'default', chipLabel: '📋 Closed', chipIcon: '📋' },
  gameStarted:     { bg: 'rgba(103, 194, 58, 0.08)',   border: 'rgba(103, 194, 58, 0.30)',  chipColor: 'success', chipLabel: '🎮 Started', chipIcon: '🎮' },
  gamePaused:      { bg: 'rgba(255, 193, 7, 0.06)',    border: 'rgba(255, 193, 7, 0.25)',  chipColor: 'warning', chipLabel: '⏸️ Paused', chipIcon: '⏸️' },
  gameResumed:     { bg: 'rgba(103, 194, 58, 0.06)',   border: 'rgba(103, 194, 58, 0.25)',  chipColor: 'success', chipLabel: '▶️ Resumed', chipIcon: '▶️' },
  gameArchived:    { bg: 'rgba(158, 158, 158, 0.06)',  border: 'rgba(158, 158, 158, 0.25)', chipColor: 'default', chipLabel: '📦 Archived', chipIcon: '📦' },
  // GM / AI
  gm:              { bg: 'rgba(121, 85, 72, 0.08)',    border: 'rgba(121, 85, 72, 0.30)',  chipColor: 'warning', chipLabel: '🤖 GM', chipIcon: '🤖' },
  narration:       { bg: 'rgba(121, 85, 72, 0.06)',    border: 'rgba(121, 85, 72, 0.25)',  chipColor: 'warning', chipLabel: '🎬 Narration', chipIcon: '🎬' },
  suggestion:      { bg: 'rgba(255, 193, 7, 0.06)',    border: 'rgba(255, 193, 7, 0.25)',  chipColor: 'warning', chipLabel: '💡 Suggestion', chipIcon: '💡' },
  consistencyCheck: { bg: 'rgba(33, 150, 243, 0.06)',  border: 'rgba(33, 150, 243, 0.25)',  chipColor: 'info', chipLabel: '🔍 Check', chipIcon: '🔍' },
  plotReview:      { bg: 'rgba(156, 39, 176, 0.06)',   border: 'rgba(156, 39, 176, 0.25)',  chipColor: 'info', chipLabel: '📋 Review', chipIcon: '📋' },
  plotThreadCreated: { bg: 'rgba(156, 39, 176, 0.06)', border: 'rgba(156, 39, 176, 0.25)',  chipColor: 'info', chipLabel: '📖 Plot', chipIcon: '📖' },
  plotThreadUpdated: { bg: 'rgba(156, 39, 176, 0.06)', border: 'rgba(156, 39, 176, 0.25)',  chipColor: 'info', chipLabel: '📖 Plot', chipIcon: '📖' },
  npcEvent:        { bg: 'rgba(121, 85, 72, 0.06)',    border: 'rgba(121, 85, 72, 0.25)',  chipColor: 'default', chipLabel: '🧙 NPC', chipIcon: '🧙' },
  // System / meta
  system:          { bg: 'rgba(158, 158, 158, 0.05)',  border: 'rgba(158, 158, 158, 0.20)', chipColor: 'default', chipLabel: '⚙️ System', chipIcon: '⚙️' },
  agentCall:       { bg: 'rgba(121, 85, 72, 0.05)',    border: 'rgba(121, 85, 72, 0.20)',  chipColor: 'default', chipLabel: '🤖 Agent', chipIcon: '🤖' },
  agentResponse:   { bg: 'rgba(76, 175, 80, 0.05)',    border: 'rgba(76, 175, 80, 0.20)',  chipColor: 'success', chipLabel: '🤖 Reply', chipIcon: '🤖' },
  toolCall:        { bg: 'rgba(255, 152, 0, 0.06)',    border: 'rgba(255, 152, 0, 0.25)',  chipColor: 'warning', chipLabel: '🔧 Tool', chipIcon: '🔧' },
  toolCallConfirmed: { bg: 'rgba(76, 175, 80, 0.06)',  border: 'rgba(76, 175, 80, 0.25)',  chipColor: 'success', chipLabel: '✅ Tool', chipIcon: '✅' },
  toolCallDenied:  { bg: 'rgba(244, 67, 54, 0.06)',    border: 'rgba(244, 67, 54, 0.25)',  chipColor: 'error', chipLabel: '❌ Tool', chipIcon: '❌' },
  playerRollRequest: { bg: 'rgba(255, 193, 7, 0.06)',  border: 'rgba(255, 193, 7, 0.25)',  chipColor: 'warning', chipLabel: '🎲 Roll?', chipIcon: '🎲' },
  playerRollConfirmed: { bg: 'rgba(103, 194, 58, 0.06)', border: 'rgba(103, 194, 58, 0.25)', chipColor: 'success', chipLabel: '✅ Roll', chipIcon: '✅' },
  playerRollDeclined: { bg: 'rgba(158, 158, 158, 0.06)', border: 'rgba(158, 158, 158, 0.25)', chipColor: 'default', chipLabel: '❌ Roll', chipIcon: '❌' },
  playerRollResult: { bg: 'rgba(255, 152, 0, 0.06)',   border: 'rgba(255, 152, 0, 0.25)',  chipColor: 'warning', chipLabel: '🎲 Result', chipIcon: '🎲' },
  stateChange:     { bg: 'rgba(158, 158, 158, 0.06)',  border: 'rgba(158, 158, 158, 0.25)', chipColor: 'default', chipLabel: '🔄 State', chipIcon: '🔄' },
  aiCombatSuggestion: { bg: 'rgba(33, 150, 243, 0.06)', border: 'rgba(33, 150, 243, 0.25)',  chipColor: 'info', chipLabel: '🤖 AI', chipIcon: '🤖' },
  aiCombatAutoResolve: { bg: 'rgba(76, 175, 80, 0.06)', border: 'rgba(76, 175, 80, 0.25)',  chipColor: 'success', chipLabel: '⚡ Auto', chipIcon: '⚡' },
};

// ==================== Message Input Types ====================

type MessageInputType = 'inGame' | 'ooc';
type MessageTarget = 'all' | 'gm' | 'player';

export default function GameRoomPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm')); // < 600px
  const { game, isLoading } = useGame(id);
  const { sessions, refetch: refetchSessions } = useSessions(id);
  const { players, refetch: refetchPlayers } = usePlayers(id);

  const { status: gmStatus, refetch: refetchGMStatus, pause: pauseGM, resume: resumeGM } = useGMStatus(id);
  const { sway, lastSway, isLoading: swayLoading } = useSway(id);
  const { isConnected, connect, on, invoke, disconnect, waitForConnection } = useGameHub();

  const [_activeTab, setActiveTab] = useState(0);
  const [hash, setHash] = useState(window.location.hash.replace('#', '') || 'chat');

  useEffect(() => {
    const handler = () => setHash(window.location.hash.replace('#', '') || 'chat');
    window.addEventListener('hashchange', handler);
    return () => window.removeEventListener('hashchange', handler);
  }, []);

  useEffect(() => {
    // Unified chat is the main interface — everything appears in chat
    // Combat and Settings are secondary views
    const tabMap: Record<string, number> = { 'chat': 0, 'combat': 1, 'settings': 2 };
    setActiveTab(tabMap[hash] ?? 0);
  }, [hash]);


  const [selectedSession, setSelectedSession] = useState<string | null>(null);
  const [openDiceDialog, setOpenDiceDialog] = useState(false);
  const [diceFormula, setDiceFormula] = useState('1d20');
  const [showDiceHistory, setShowDiceHistory] = useState(false);
  const [createSessionOpen, setCreateSessionOpen] = useState(false);
  const [sessionTitle, setSessionTitle] = useState('');
  const [errorState, setErrorState] = useState<string | null>(null);
  const [successState, setSuccessState] = useState<string | null>(null);
  const [_activeCombats, _setActiveCombats] = useState<any[]>([]);
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
  const { messages, isLoadingMore, hasMore, loadInitial, loadMoreOldest, addMessage, updateMessage } = useMessagesInfiniteScroll(id, selectedSession || undefined);
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
  }, [id, user]);

  // Auto-start the game if it's in Draft status
  useEffect(() => {
    if (id && game && game.status === 'Draft') {
      api.startGame(id).catch((e) => {
        console.error('Failed to auto-start game:', e);
      });
    }
  }, [id, game?.status]);

  // Set first active session as default
  useEffect(() => {
    if (sessions.length > 0 && !selectedSession) {
      const activeSession = sessions.find(s => !s.endedAt);
      setSelectedSession(activeSession?.id || sessions[0].id);
    }
  }, [sessions, selectedSession]);

  // Load initial messages when session changes
  useEffect(() => {
    if (id && selectedSession) {
      loadInitial();
    }
  }, [id, selectedSession, loadInitial]);

  // Listen for incoming messages — all funnel into unified chat
  useEffect(() => {
    if (!isConnected) return;

    on('NewMessage', (msg: any) => {
      addMessage({
        id: msg.Id,
        type: msg.IsOOC ? 'oocPublic' : 'inGamePublic',
        content: msg.Content,
        senderName: msg.PlayerId ? 'Player' : 'AI-GM',
        senderRole: msg.PlayerId ? 'Player' : 'GM',
        timestamp: msg.CreatedAt,
      });
    });

    on('NewOOCMessage', (msg: any) => {
      addMessage({
        id: `ooc-${msg.Id}`,
        type: 'oocPublic',
        content: msg.Content,
        senderName: msg.PlayerId ? 'Player' : 'AI-GM',
        senderRole: msg.PlayerId ? 'Player' : 'GM',
        timestamp: msg.CreatedAt,
      });
    });

    on('NewWhisper', (whisper: any) => {
      const isInGame = whisper.Type === WhisperType.InGamePlayerToGM ||
                       whisper.Type === WhisperType.InGameGMToPlayer;
      addMessage({
        id: `whisper-${whisper.Id}`,
        type: isInGame ? 'inGameWhisper' : 'oocWhisper',
        content: whisper.Content,
        senderName: whisper.FromCharacter,
        senderRole: whisper.FromRole,
        timestamp: whisper.CreatedAt,
        isWhisper: true,
        whisperTo: whisper.Targets === 'gm' ? 'GM' : whisper.Targets,
      });
    });

    on('NewOOCWhisper', (whisper: any) => {
      addMessage({
        id: `ooc-whisper-${whisper.Id}`,
        type: 'oocWhisper',
        content: whisper.Content,
        senderName: whisper.FromCharacter,
        senderRole: whisper.FromRole,
        timestamp: whisper.CreatedAt,
        isWhisper: true,
        whisperTo: whisper.Targets === 'gm' ? 'GM' : whisper.Targets,
      });
    });

    on('DiceRollResult', (result: any) => {
      addMessage({
        id: `dice-${Date.now()}`,
        type: 'dice',
        content: `🎲 **${result.Formula}** → **${result.Total}**${result.FinalRolls?.length ? ` (kept: [${result.FinalRolls.join(',')}])` : ''}${result.Modifier ? ` (modifier: ${result.Modifier})` : ''}`,
        senderName: result.PlayerId ? 'Player' : 'System',
        senderRole: result.PlayerId ? 'Player' : 'System',
        timestamp: result.Timestamp,
        isSystem: true,
        diceFormula: result.Formula,
        diceTotal: result.Total,
        diceRolls: result.Rolls,
      });
      setShowDiceHistory(true);
    });

    on('PlayerJoined', () => {
      refetchPlayers();
    });

    on('PlayerLeft', () => {
      refetchPlayers();
    });

    on('SkillCheckResult', (result: any) => {
      addMessage({
        id: `skill-${Date.now()}`,
        type: 'skillCheck',
        content: `📋 **${result.Skill} Check** vs DC ${result.DC}: d20(${result.DiceRoll})+${result.Modifier >= 0 ? '+' : ''}${result.Modifier} = **${result.Total}** → ${result.Success ? '✅ Success' : '❌ Failure'}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: result.RolledAt,
        isSystem: true,
        skill: result.Skill,
        skillDC: result.DC,
        skillResult: result.Success ? 'success' : 'failure',
      });
    });

    on('AttackResult', (result: any) => {
      addMessage({
        id: `attack-${Date.now()}`,
        type: 'attack',
        content: `⚔️ **${result.Weapon}** vs **${result.Target}**: d20(${result.AttackRoll}) vs AC ${result.AC} → ${result.Hit ? `✅ **HIT!** ${result.DamageTotal} damage` : '❌ **MISS**'}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: result.RolledAt,
        isSystem: true,
        attackWeapon: result.Weapon,
        attackTarget: result.Target,
        attackHit: result.Hit,
        attackDamage: result.DamageTotal,
      });
    });

    // ==================== Combat Events ====================

    on('CombatStarted', (event: any) => {
      addMessage({
        id: `combat-start-${event.combatId}`,
        type: 'combatStart',
        content: `⚔️ **Combat Started**: ${event.name || 'An unexpected encounter!'} (${event.participants?.length || 0} participants)`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: event.startedAt || new Date().toISOString(),
        isSystem: true,
        combatName: event.name,
      });
    });

    on('CombatEnded', (event: any) => {
      addMessage({
        id: `combat-end-${event.combatId}`,
        type: 'combatEnd',
        content: `⚔️ **Combat Ended**: ${event.result || 'No result'}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: event.endedAt || new Date().toISOString(),
        isSystem: true,
      });
    });

    on('CombatPaused', () => {
      addMessage({
        id: `combat-pause-${Date.now()}`,
        type: 'combatPause',
        content: '⏸️ Combat paused',
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
      });
    });

    on('CombatResumed', () => {
      addMessage({
        id: `combat-resume-${Date.now()}`,
        type: 'combatResume',
        content: '▶️ Combat resumed',
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
      });
    });

    on('CombatParticipantAdded', (event: any) => {
      addMessage({
        id: `participant-${event.participantId}-${Date.now()}`,
        type: 'participantAdded',
        content: `➕ **${event.displayName}** (${event.participantType}) joins combat — HP: ${event.currentHP}/${event.maxHP}, AC: ${event.ac}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        participantType: event.participantType,
        hp: event.currentHP,
        maxHP: event.maxHP,
        ac: event.ac,
      });
    });

    on('CombatParticipantRemoved', (event: any) => {
      addMessage({
        id: `participant-removed-${event.participantId}-${Date.now()}`,
        type: 'participantRemoved',
        content: '➖ Participant removed from combat',
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
      });
    });

    on('InitiativeRolled', (event: any) => {
      addMessage({
        id: `initiative-${event.participantId}-${Date.now()}`,
        type: 'initiative',
        content: `🎲 **${event.displayName}** rolls initiative: **${event.initiative}**`,
        senderName: event.displayName,
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        initiative: event.initiative,
        diceRolls: event.rolls,
      });
    });

    on('InitiativeComplete', (event: any) => {
      addMessage({
        id: `initiative-complete-${Date.now()}`,
        type: 'initiativeComplete',
        content: `📊 **Initiative order**: ${event.turnOrder}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
      });
    });

    on('TurnAdvanced', (event: any) => {
      addMessage({
        id: `turn-${Date.now()}`,
        type: 'turnAdvanced',
        content: `⏩ **Turn ${event.currentRound}**: ${event.displayName}'s turn`,
        senderName: event.displayName,
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        hp: event.currentHP,
        maxHP: event.maxHP,
        ac: event.ac,
        initiative: event.initiative,
      });
    });

    on('TurnRetreated', (event: any) => {
      addMessage({
        id: `turn-retreat-${Date.now()}`,
        type: 'turnRetreated',
        content: `↩️ **Turn Retreated**: ${event.displayName}'s turn`,
        senderName: event.displayName,
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
      });
    });

    on('CombatDamage', (event: any) => {
      addMessage({
        id: `damage-${Date.now()}`,
        type: 'damage',
        content: `💥 **Damage**: ${event.damage}${event.source ? ` from ${event.source}` : ''} — HP: ${event.hp}/${event.maxHP}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        damage: event.damage,
        hp: event.hp,
        maxHP: event.maxHP,
      });
    });

    on('CombatHeal', (event: any) => {
      addMessage({
        id: `heal-${Date.now()}`,
        type: 'heal',
        content: `💚 **Heal**: ${event.amount} HP${event.source ? ` from ${event.source}` : ''} — HP: ${event.hp}/${event.maxHP}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        healAmount: event.amount,
        hp: event.hp,
        maxHP: event.maxHP,
      });
    });

    on('ConditionApplied', (event: any) => {
      addMessage({
        id: `condition-${Date.now()}`,
        type: 'conditionApplied',
        content: `🔴 **Condition Applied**: ${event.conditionName}${event.duration ? ` (duration: ${event.duration})` : ''}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        conditionName: event.conditionName,
        conditionDuration: event.duration,
      });
    });

    on('ConditionRemoved', (event: any) => {
      addMessage({
        id: `condition-removed-${Date.now()}`,
        type: 'conditionRemoved',
        content: `🟢 **Condition Removed**: ${event.conditionName}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        conditionName: event.conditionName,
      });
    });

    on('CombatDeathSave', (event: any) => {
      addMessage({
        id: `death-save-${Date.now()}`,
        type: 'deathSave',
        content: `💀 **Death Save**: ${event.participant} — ${event.successes} successes, ${event.failures} failures${event.isStabilized ? ' — Stabilized!' : event.isDead ? ' — Dead!' : ''}`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.participant,
      });
    });

    on('ActionSpent', (event: any) => {
      addMessage({
        id: `action-${Date.now()}`,
        type: 'actionSpent',
        content: `🎯 **Action spent**: ${event.displayName} has ${event.actionsRemaining} actions remaining`,
        senderName: event.displayName,
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        actionsRemaining: event.actionsRemaining,
        bonusActionsRemaining: event.bonusActionsRemaining,
        reactionsRemaining: event.reactionsRemaining,
        movementsRemaining: event.movementsRemaining,
      });
    });

    on('BonusActionSpent', (event: any) => {
      addMessage({
        id: `bonus-action-${Date.now()}`,
        type: 'bonusActionSpent',
        content: `⚡ **Bonus Action spent**: ${event.displayName} has ${event.bonusActionsRemaining} remaining`,
        senderName: event.displayName,
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        bonusActionsRemaining: event.bonusActionsRemaining,
      });
    });

    on('ReactionSpent', (event: any) => {
      addMessage({
        id: `reaction-${Date.now()}`,
        type: 'reactionSpent',
        content: `🔄 **Reaction spent**: ${event.displayName} has ${event.reactionsRemaining} remaining`,
        senderName: event.displayName,
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        reactionsRemaining: event.reactionsRemaining,
      });
    });

    on('MovementSpent', (event: any) => {
      addMessage({
        id: `movement-${Date.now()}`,
        type: 'movementSpent',
        content: `🚶 **Movement spent**: ${event.displayName} has ${event.movementsRemaining} remaining`,
        senderName: event.displayName,
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        movementsRemaining: event.movementsRemaining,
      });
    });

    on('ActionsRefreshed', (event: any) => {
      addMessage({
        id: `actions-refreshed-${Date.now()}`,
        type: 'actionsRefreshed',
        content: `🔄 **Actions refreshed**: ${event.displayName} — Actions: ${event.actionsRemaining}, Bonus: ${event.bonusActionsRemaining}, Reactions: ${event.reactionsRemaining}, Movement: ${event.movementsRemaining}`,
        senderName: event.displayName,
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.displayName,
        actionsRemaining: event.actionsRemaining,
        bonusActionsRemaining: event.bonusActionsRemaining,
        reactionsRemaining: event.reactionsRemaining,
        movementsRemaining: event.movementsRemaining,
      });
    });

    // ==================== Player Lifecycle ====================

    on('PlayerDisconnected', (event: any) => {
      addMessage({
        id: `disconnect-${event.playerId}-${Date.now()}`,
        type: 'playerDisconnected',
        content: `⚠️ **${event.characterName}** has been disconnected`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: event.disconnectedAt || new Date().toISOString(),
        isSystem: true,
        participantName: event.characterName,
      });
    });

    on('PlayerReconnected', (event: any) => {
      addMessage({
        id: `reconnect-${event.playerId}-${Date.now()}`,
        type: 'playerReconnected',
        content: `✅ **${event.characterName}** has reconnected`,
        senderName: 'System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
        participantName: event.characterName,
      });
    });

    on('Error', (err: any) => {
      setErrorState(err.message);
    });

    on('AgentCallStarted', (call: any) => {
      addMessage({
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
      });
    });

    on('AgentCallCompleted', (call: any) => {
      // Update the running agent call message
      updateMessage(`agent-start-${call.Id}`, m => ({
        ...m,
        agentStatus: call.Status,
        content: `${m.content} → ${call.Status}`,
      }));
      // Also add a completion message
      addMessage({
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
      });
    });

    // ==================== Tool Call Events ====================

    on('ToolCallNotification', (event: any) => {
      addMessage({
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
      });
      refetchToolCalls();
    });

    on('ToolCallConfirmed', async (event: any) => {
      if (event.approved) {
        // Tool was approved — add result as narrative message
        if (event.outputMessage) {
          addMessage({
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
          });
        }
        // If this was a narration, also post narrative to chat
        if (event.toolName === 'narrate' && event.result) {
          try {
            const parsed = JSON.parse(event.result as string);
            if (parsed.context) {
              addMessage({
                id: `narrative-${Date.now()}`,
                type: 'inGamePublic',
                content: parsed.context,
                senderName: '🤖 AI-GM',
                senderRole: 'GM',
                timestamp: new Date().toISOString(),
              });
            }
          } catch { /* ignore parse errors */ }
        }
      } else {
        addMessage({
          id: `tool-denied-${event.toolCallId}`,
          type: 'system',
          content: `❌ ${event.toolName} denied by GM`,
          senderName: '⚙️ System',
          senderRole: 'System',
          timestamp: new Date().toISOString(),
          isSystem: true,
        });
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
      addMessage({
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
      });
    });

    on('PlayerRollDeclined', (event: any) => {
      addMessage({
        id: `roll-declined-${event.toolCallId}`,
        type: 'system',
        content: `${event.playerName} declined to roll ${event.skill}`,
        senderName: '⚙️ System',
        senderRole: 'System',
        timestamp: new Date().toISOString(),
        isSystem: true,
      });
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
        if (c && c.length > 0) _setActiveCombats(c);
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



  const handleDiceRoll = async () => {
    if (!selectedSession) return;
    try {
      const result = await invoke('RollDice', selectedSession, diceFormula, user?.id);
      addMessage({
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
      });
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
      // Wait for hub connection to be active before invoking
      if (!isConnected) {
        await waitForConnection();
      }

      // Check if the current user is the game creator
      if (game?.creatorId === user.id) {
        // Creator can create a character directly without being a player
        await invoke('CreateCharacterAsCreator', id, user.id, JSON.stringify(characterData));
        setSuccessState(`Character '${characterData.name}' created!`);
        setTimeout(() => setSuccessState(null), 2000);
        setShowCharacterWizard(false);
        return;
      }

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
      addMessage({
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
      });
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
      addMessage({
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
      });
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

  // ==================== Manual LLM Trigger Handlers ====================

  const handleTriggerNarrate = async () => {
    if (!id) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      await api.triggerNarrate(id);
      setSuccessState('Narrative queued');
      setTimeout(() => setSuccessState(null), 3000);
      refetchGMStatus();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleTriggerSuggest = async () => {
    if (!id) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      await api.triggerSuggest(id);
      setSuccessState('Suggestions queued');
      setTimeout(() => setSuccessState(null), 3000);
      refetchGMStatus();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleTriggerConsistency = async () => {
    if (!id) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      await api.triggerConsistency(id);
      setSuccessState('Consistency check queued');
      setTimeout(() => setSuccessState(null), 3000);
      refetchGMStatus();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleTriggerReview = async () => {
    if (!id) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      await api.triggerReview(id);
      setSuccessState('Plot review triggered');
      setTimeout(() => setSuccessState(null), 3000);
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
  const isCreator = game?.creatorId === user?.id || players.some((p: any) => p.role === 'Creator');

  return (
    <Box>
      {/* Game Info Header */}
      <Box sx={{
        display: 'flex',
        flexDirection: isMobile ? 'column' : 'row',
        justifyContent: 'space-between',
        alignItems: isMobile ? 'flex-start' : 'center',
        mb: 2,
        gap: isMobile ? 1 : 2,
      }}>
        <Box sx={{ flex: isMobile ? 0 : 1, minWidth: 0 }}>
          <Typography variant={isMobile ? 'h6' : 'h5'} noWrap>{game.name}</Typography>
          <Box sx={{ display: 'flex', gap: 0.5, mt: 0.5, flexWrap: 'wrap' }}>
            <Chip label={game.systemId} size="small" />
            {game.language && <Chip label={`🌐 ${game.language}`} size="small" variant="outlined" color="info" />}
            <Chip label={game.status} size="small" color={game.status === 'Active' ? 'success' : 'default'} />
            {game.llmPresetName && <Chip label={game.llmPresetName} size="small" variant="outlined" />}
            {game.inviteCode && (
              <Chip label={`Code: ${game.inviteCode}`} size="small" variant="outlined" color="primary" />
            )}
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
        <Box sx={{ display: 'flex', gap: 1, flexShrink: 0 }}>
          {game.status === 'Active' && gmStatus?.status === 'running' && (
            <Button size="small" variant="outlined" onClick={handlePauseGM}>Pause GM</Button>
          )}
          {game.status === 'Active' && gmStatus?.status === 'paused' && (
            <Button size="small" variant="outlined" onClick={handleResumeGM}>Resume GM</Button>
          )}
          <Button variant="outlined" color="error" size={isMobile ? 'medium' : 'small'} startIcon={<LeaveIcon />} onClick={handleLeave}>
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

      {/* Tab Content (tabs are now in the side panel) */}
      <Box sx={{
        display: 'flex',
        flexDirection: isMobile ? 'column' : 'row',
        gap: isMobile ? 1 : 2,
      }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          {hash === 'chat' && (
            <UnifiedChatPanel
              messages={messages}
              isLoadingMore={isLoadingMore}
              hasMore={hasMore}
              loadMoreOldest={loadMoreOldest}
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
              isMobile={isMobile}
              onOpenCharacter={() => setShowCharacterWizard(true)}
            />
          )}

          {hash === 'combat' && (
            <CombatTab gameId={id || ''} />
          )}

          {/* Note: Players, Characters, Actions, Dice History, Combat Log, GM Tools, and Agent Calls
              are all visible in the unified chat above. Use the chat to see everything.
              Quick links to detailed views are available in the side panel. */}

          {hash === 'settings' && (
            <SettingsTab
              game={game}
              sessions={sessions}
              onNewSession={() => setCreateSessionOpen(true)}
              onOpenCharacterWizard={() => setShowCharacterWizard(true)}
              isCreator={isCreator}
              gameId={id}
              onTriggerNarrate={handleTriggerNarrate}
              onTriggerSuggest={handleTriggerSuggest}
              onTriggerConsistency={handleTriggerConsistency}
              onTriggerReview={handleTriggerReview}
            />
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
    </Box>
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
  isLoadingMore: boolean;
  hasMore: boolean;
  loadMoreOldest: () => void;
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
  isMobile: boolean;
  onOpenCharacter: () => void;
}

function UnifiedChatPanel({
  messages, isLoadingMore, hasMore, loadMoreOldest,
  inputType, setInputType, inputTarget, setInputTarget,
  whisperTargetPlayer, setWhisperTargetPlayer, whisperInput, setWhisperInput,
  onSend, onDiceRoll, onSkillCheck, showDiceHistory, setShowDiceHistory: _setShowDiceHistory,
  messagesEndRef, players, isConnected: _isConnected, activeSession, isCreator, isMobile,
  onOpenCharacter
}: UnifiedChatPanelProps) {
  const [quickSkill, setQuickSkill] = useState('Perception');
  const [quickDC, setQuickDC] = useState(15);
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  // Scroll detection: load older messages when scrolling to the top
  const handleScroll = useCallback(() => {
    const el = scrollContainerRef.current;
    if (!el) return;
    // Within 100px of the top, load more
    if (el.scrollTop < 100 && hasMore && !isLoadingMore) {
      loadMoreOldest();
    }
  }, [hasMore, isLoadingMore, loadMoreOldest]);

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
    <Paper sx={{
      height: isMobile ? 'calc(100dvh - 220px)' : '75vh',
      minHeight: isMobile ? 300 : 400,
      display: 'flex',
      flexDirection: 'column',
    }}>
      {/* ===== Message Type & Receiver Selector ===== */}
      <Box sx={{
        p: isMobile ? 1 : 1.5,
        borderBottom: 1,
        borderColor: 'divider',
        display: 'flex',
        gap: 1,
        alignItems: 'center',
        flexWrap: 'wrap',
        bgcolor: 'background.paper',
      }}>
        {/* Message Type Toggle */}
        <Box sx={{ display: 'flex', bgcolor: 'background.default', borderRadius: 1, overflow: 'hidden' }}>
          <Button
            size="small"
            onClick={() => setInputType('inGame')}
            sx={{
              bgcolor: inputType === 'inGame' ? 'success.lighter' : 'transparent',
              color: inputType === 'inGame' ? 'success.dark' : 'text.secondary',
              minWidth: 80,
              px: isMobile ? 1.5 : 2,
              fontSize: 11,
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
              minWidth: 80,
              px: isMobile ? 1.5 : 2,
              fontSize: 11,
              fontWeight: inputType === 'ooc' ? 600 : 400,
              '&:hover': { bgcolor: inputType === 'ooc' ? 'info.lighter' : 'action.hover' }
            }}
          >
            📢 OOC
          </Button>
        </Box>

        {/* Receiver Selector */}
        <FormControl size="small" sx={{ minWidth: isMobile ? 110 : 140 }}>
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
        <Chip label="📝 Character" size="small" clickable onClick={onOpenCharacter} sx={{ fontSize: 11 }} />
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <TextField
            size="small"
            value={quickSkill}
            onChange={e => setQuickSkill(e.target.value)}
            sx={{ width: isMobile ? 80 : 100, '& .MuiInputBase-root': { height: 32, fontSize: 12 } }}
            placeholder="Skill"
          />
          <TextField
            size="small"
            type="number"
            value={quickDC}
            onChange={e => setQuickDC(parseInt(e.target.value) || 0)}
            sx={{ width: isMobile ? 50 : 60, '& .MuiInputBase-root': { height: 32, fontSize: 12 } }}
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
        <Chip label="📝 Character" size="small" clickable onClick={onOpenCharacter} sx={{ fontSize: 11 }} />
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
      <Box sx={{ flex: 1, overflow: 'auto', p: isMobile ? 1 : 1.5 }} ref={scrollContainerRef} onScroll={handleScroll}>
        {/* Loading indicator for older messages */}
        {isLoadingMore && (
          <Typography variant="caption" color="text.secondary" sx={{ textAlign: 'center', py: 1, display: 'block' }}>
            Loading older messages...
          </Typography>
        )}
        {messages.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 8 }}>
            No messages yet. Start the conversation!
          </Typography>
        ) : (
          messages.map((msg) => (
            <MessageBubble key={msg.id} msg={msg} />
          ))
        )}
        <div ref={messagesEndRef} />
      </Box>

      {/* ===== Input Area ===== */}
      <Box sx={{
        p: isMobile ? 1 : 1.5,
        borderTop: 1,
        borderColor: 'divider',
        bgcolor: 'background.paper'
      }}>
        {/* Player whisper confirmation */}
        {whisperTargetPlayer && (
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1, gap: 1 }}>
            <Typography variant="caption" color="text.secondary" sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', flex: 1 }}>
              📩 Whispering to: {players.find(p => p.id === whisperTargetPlayer)?.characterName}
            </Typography>
            <Button size="small" onClick={() => setWhisperTargetPlayer(null)}>Clear</Button>
          </Box>
        )}

        <Box sx={{ display: 'flex', gap: isMobile ? 0.5 : 1 }}>
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
            sx={{ minWidth: isMobile ? 60 : 80 }}
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
  const isSystem = msg.isSystem || ['dice', 'skillCheck', 'attack', 'spellCast', 'combatStart', 'combatEnd', 'combatPause', 'combatResume',
    'initiative', 'initiativeComplete', 'turnAdvanced', 'turnRetreated', 'turnSet', 'damage', 'heal', 'deathSave',
    'conditionApplied', 'conditionRemoved', 'xpGranted', 'levelUp', 'sanLoss', 'sanRecovery', 'sanCheck',
    'actionSpent', 'bonusActionSpent', 'reactionSpent', 'movementSpent', 'actionsRefreshed', 'participantAdded',
    'participantRemoved', 'gridSet', 'positionSet', 'combatMove', 'itemAdded', 'itemRemoved', 'itemEquipped',
    'itemUnequipped', 'playerJoined', 'playerLeft', 'playerDisconnected', 'playerReconnected', 'playerRoleChanged',
    'characterCreated', 'characterUpdated', 'sessionCreated', 'sessionClosed', 'gameStarted', 'gamePaused',
    'gameResumed', 'gameArchived', 'system', 'agentCall', 'agentResponse', 'toolCall', 'toolCallConfirmed',
    'toolCallDenied', 'playerRollRequest', 'playerRollConfirmed', 'playerRollDeclined', 'playerRollResult',
    'stateChange', 'aiCombatSuggestion', 'aiCombatAutoResolve'].includes(msg.type);

  // Determine if this is a system/notification message (rendered more subtly)
  const isNotification = ['combatStart', 'combatEnd', 'combatPause', 'combatResume', 'initiative', 'initiativeComplete',
    'turnAdvanced', 'turnRetreated', 'turnSet', 'participantAdded', 'participantRemoved', 'playerJoined', 'playerLeft',
    'playerDisconnected', 'playerReconnected', 'playerRoleChanged', 'characterCreated', 'characterUpdated',
    'sessionCreated', 'sessionClosed', 'gameStarted', 'gamePaused', 'gameResumed', 'gameArchived', 'stateChange',
    'gridSet', 'positionSet', 'combatMove', 'itemAdded', 'itemRemoved', 'itemEquipped', 'itemUnequipped',
    'actionSpent', 'bonusActionSpent', 'reactionSpent', 'movementSpent', 'actionsRefreshed', 'xpGranted', 'levelUp',
    'sanLoss', 'sanRecovery', 'sanCheck', 'deathSave', 'conditionApplied', 'conditionRemoved',
    'toolCall', 'toolCallConfirmed', 'toolCallDenied', 'playerRollRequest', 'playerRollConfirmed', 'playerRollDeclined',
    'aiCombatSuggestion', 'aiCombatAutoResolve'].includes(msg.type);

  return (
    <Box sx={{
      mb: isNotification ? 0.5 : 1,
      p: isNotification ? 0.75 : 1.5,
      borderRadius: 2,
      bgcolor: style.bg,
      borderLeft: `3px solid ${style.border}`,
      opacity: isNotification ? 0.85 : 1,
    }}>
      {/* Header: Type chip + sender + time */}
      <Box sx={{ display: 'flex', gap: 0.75, alignItems: 'center', flexWrap: 'wrap', mb: isNotification ? 0.25 : 0.5 }}>
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

      {/* Content — markdown for text messages, plain for system messages */}
      {MARKDOWN_TYPES.has(msg.type) && hasMarkdownSyntax(msg.content) ? (
        <Box sx={{
          color: isWhisper ? 'text.secondary' : 'text.primary',
          fontStyle: isWhisper ? 'italic' : 'normal',
          wordBreak: 'break-word',
        }}>
          <MarkdownRenderer content={msg.content} compact />
        </Box>
      ) : (
        <Typography variant="body2" sx={{
          color: isSystem ? 'text.secondary' : 'text.primary',
          fontStyle: isWhisper ? 'italic' : 'normal',
          wordBreak: 'break-word',
        }}>
          {msg.content}
        </Typography>
      )}

      {/* Extra info for dice */}
      {msg.type === 'dice' && msg.diceRolls && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Rolls: [{msg.diceRolls.join(', ')}]
        </Typography>
      )}

      {/* Extra info for skill checks */}
      {msg.type === 'skillCheck' && msg.skillResult && (
        <Chip
          label={msg.skillResult === 'success' ? '✓ Success' : '✗ Failure'}
          size="small"
          color={msg.skillResult === 'success' ? 'success' : 'error'}
          sx={{ mt: 0.5, height: 20, fontSize: 10 }}
        />
      )}

      {/* Extra info for attacks */}
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

      {/* Extra info for combat participants */}
      {msg.type === 'participantAdded' && msg.participantType && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Type: {msg.participantType}{msg.hp != null ? ` · HP: ${msg.hp}/${msg.maxHP}` : ''}{msg.ac != null ? ` · AC: ${msg.ac}` : ''}
        </Typography>
      )}

      {/* Extra info for initiative */}
      {msg.type === 'initiative' && msg.diceRolls && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Rolls: [{msg.diceRolls.join(', ')}]
        </Typography>
      )}

      {/* Extra info for conditions */}
      {msg.type === 'conditionApplied' && msg.conditionDuration != null && (
        <Chip
          label={`Duration: ${msg.conditionDuration}`}
          size="small"
          color="warning"
          sx={{ mt: 0.5, height: 20, fontSize: 10 }}
        />
      )}

      {/* Extra info for damage/heal */}
      {(msg.type === 'damage' || msg.type === 'heal') && msg.hp != null && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          HP: {msg.hp}/{msg.maxHP}
        </Typography>
      )}

      {/* Extra info for action economy */}
      {(msg.type === 'actionSpent' || msg.type === 'bonusActionSpent' || msg.type === 'reactionSpent' || msg.type === 'movementSpent') && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          {msg.actionsRemaining != null ? `Actions: ${msg.actionsRemaining} ` : ''}
          {msg.bonusActionsRemaining != null ? `Bonus: ${msg.bonusActionsRemaining} ` : ''}
          {msg.reactionsRemaining != null ? `Reactions: ${msg.reactionsRemaining} ` : ''}
          {msg.movementsRemaining != null ? `Movement: ${msg.movementsRemaining}` : ''}
        </Typography>
      )}

      {/* Extra info for agent calls */}
      {msg.type === 'agentCall' && msg.agentStatus && (
        <Chip
          label={msg.agentStatus}
          size="small"
          color={msg.agentStatus === 'Completed' ? 'success' : msg.agentStatus === 'Running' ? 'warning' : 'error'}
          sx={{ mt: 0.5, height: 20, fontSize: 10 }}
        />
      )}

      {/* Extra info for player roll */}
      {msg.type === 'playerRollRequest' && msg.skill && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Skill: {msg.skill} · DC: {msg.skillDC}
        </Typography>
      )}
    </Box>
  );
}

// ==================== Sub-Components ====================

// Note: Players, Characters, Actions, Dice History, Combat Log, GM Tools, and Agent Calls
// are all visible in the unified chat above. No separate tabs needed.

function SettingsTab({ game, sessions, onNewSession, onOpenCharacterWizard, isCreator, gameId, onTriggerNarrate, onTriggerSuggest, onTriggerConsistency, onTriggerReview }: any) {
  const navigate = useNavigate();
  const [language, setLanguage] = useState(game.language || 'English');
  const [saving, setSaving] = useState(false);

  const handleSaveLanguage = async () => {
    if (!gameId || saving) return;
    setSaving(true);
    try {
      await api.updateGameLanguage(gameId, language);
    } catch (e) {
      console.error('Failed to update game language:', e);
    } finally {
      setSaving(false);
    }
  };

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

      {/* Character Creation */}
      <Box sx={{ mb: 3 }}>
        <Typography variant="subtitle1" gutterBottom>📝 Character</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
          Create and manage your in-game character. You can edit it later from the admin panel.
        </Typography>
        <Button
          variant="outlined"
          onClick={onOpenCharacterWizard}
          startIcon={<PeopleIcon />}
        >
          Create New Character
        </Button>
      </Box>

      <Divider sx={{ my: 2 }} />

      {isCreator && (
        <>
          <Divider sx={{ my: 2 }} />
          <Box sx={{ mb: 2 }}>
            <Typography variant="subtitle1">Narration Language</Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
              The language the AI-GM uses for all narrative output. NPCs speaking in their native unknown language may be described in English for player comprehension.
            </Typography>
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 1 }}>
              {['English', 'Spanish', 'French', 'German', 'Italian', 'Portuguese', 'Japanese', 'Korean', 'Chinese', 'Ukrainian', 'Polish', 'Dutch', 'Swedish', 'Norwegian', 'Finnish', 'Danish', 'Greek', 'Turkish', 'Arabic', 'Hindi'].map(lang => (
                <Chip
                  key={lang}
                  label={lang}
                  size="small"
                  clickable
                  onClick={() => setLanguage(lang)}
                  color={language === lang ? 'primary' : 'default'}
                  variant={language === lang ? 'filled' : 'outlined'}
                  sx={{ fontSize: 11 }}
                />
              ))}
            </Box>
            <TextField
              fullWidth
              size="small"
              placeholder="Or type a custom language (e.g., Esperanto, Klingon, etc.)"
              value={language}
              onChange={e => setLanguage(e.target.value)}
              sx={{ bgcolor: 'background.paper' }}
            />
            <Button
              size="small"
              variant="outlined"
              onClick={handleSaveLanguage}
              disabled={saving || language === game.language}
              sx={{ mt: 1 }}
            >
              {saving ? 'Saving...' : 'Save Language'}
            </Button>
          </Box>
        </>
      )}

      {isCreator && (
        <>
          <Divider sx={{ my: 2 }} />
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mb: 1 }}>
            <Typography variant="subtitle1">Admin Panel</Typography>
            <Button
              size="small"
              variant="contained"
              color="secondary"
              onClick={() => navigate(`/admin/${gameId}`)}
            >
              Open Admin
            </Button>
          </Box>
        </>
      )}

      <Divider sx={{ my: 2 }} />

      {/* ==================== Manual LLM Triggers ==================== */}
      <Box sx={{ mb: 3 }}>
        <Typography variant="subtitle1" gutterBottom>
          🤖 Manual LLM Triggers
        </Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
          Use these buttons to force the AI-GM to generate content when it's not doing so automatically.
        </Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          <Button
            variant="outlined"
            size="small"
            onClick={onTriggerNarrate}
            disabled={game.status !== 'Active'}
            sx={{ minWidth: 140 }}
          >
            🎬 Narrate
          </Button>
          <Button
            variant="outlined"
            size="small"
            onClick={onTriggerSuggest}
            disabled={game.status !== 'Active'}
            sx={{ minWidth: 140 }}
          >
            💡 Suggestions
          </Button>
          <Button
            variant="outlined"
            size="small"
            onClick={onTriggerConsistency}
            disabled={game.status !== 'Active'}
            sx={{ minWidth: 140 }}
          >
            🔍 Consistency Check
          </Button>
          <Button
            variant="outlined"
            size="small"
            onClick={onTriggerReview}
            disabled={game.status !== 'Active'}
            sx={{ minWidth: 140 }}
          >
            📋 Plot Review
          </Button>
        </Box>
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
