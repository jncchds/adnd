import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../api/hooks/useAuth';
import { useGame } from '../api/hooks/useGameDetail';
import { useToolCalls } from '../api/hooks/useAgent';
import { useMessagesInfiniteScroll } from '../api/hooks/useMessages';
import { useGameHub } from '../api/hooks/useHub';
import { usePlayers } from '../api/hooks/useSessionPlayers';
import { combatGetCombats } from '../api/combat/combatApi';
import { api } from '../api/client';
import ToolCallBanner from '../components/ToolCallBanner';
import ChatPanel from '../components/chat/ChatPanel';
import { Box, Paper, Typography, Chip, IconButton, Collapse, TextField, MenuItem, Select, FormControl, InputLabel, Button } from '@mui/material';
import { Send as SendIcon, ExpandMore, ExpandLess } from '@mui/icons-material';

// ==================== Types ====================

type MessageInputType = 'inGame' | 'ooc';
type MessageTarget = 'all' | 'gm' | 'player';

interface CombatParticipant {
  id: string;
  displayName: string;
  participantType: string;
  currentHP: number;
  maxHP: number;
  ac: number;
  initiative: number;
  conditions: Array<{ name: string; duration?: number }>;
  isCurrentTurn: boolean;
}

interface ActiveCombat {
  combatId: string;
  name: string;
  status: string;
  currentRound: number;
  currentTurnIndex: number;
  participants: CombatParticipant[];
}

// ==================== Collapsible Combat Panel ====================

function CollapsibleCombatPanel({
  combat,
  gameId,
}: {
  combat: ActiveCombat | null;
  gameId: string;
}) {
  const [collapsed, setCollapsed] = useState(false);

  if (!combat || combat.status === 'Finished') return null;

  const hpColor = (current: number, max: number): string => {
    const pct = current / max;
    if (pct > 0.75) return '#4caf50';
    if (pct > 0.25) return '#ff9800';
    return '#f44336';
  };

  const sortedParticipants = [...combat.participants]
    .sort((a, b) => b.initiative - a.initiative || b.id.localeCompare(a.id));

  return (
    <Paper
      sx={{
        m: 1.5,
        borderRadius: 2,
        overflow: 'hidden',
        border: '1px solid rgba(244, 67, 54, 0.3)',
        bgcolor: 'rgba(244, 67, 54, 0.04)',
      }}
    >
      {/* Header */}
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          px: 1.5,
          py: 0.75,
          bgcolor: 'rgba(244, 67, 54, 0.08)',
          borderBottom: '1px solid rgba(244, 67, 54, 0.15)',
          cursor: 'pointer',
          userSelect: 'none',
        }}
        onClick={() => setCollapsed(!collapsed)}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, color: 'error.main' }}>
            ⚔️ Combat: {combat.name || 'Unnamed'}
          </Typography>
          <Chip
            label={combat.status === 'Active' ? '⏳ Active' : '⏸️ Paused'}
            size="small"
            sx={{
              height: 20,
              fontSize: 10,
              bgcolor: combat.status === 'Active' ? 'rgba(255, 152, 0, 0.15)' : 'rgba(255, 193, 7, 0.15)',
              color: combat.status === 'Active' ? 'orange' : 'warning.dark',
              fontWeight: 600,
            }}
          />
          <Typography variant="caption" color="text.secondary">
            Round {combat.currentRound}
          </Typography>
        </Box>
        <IconButton size="small" sx={{ color: 'text.secondary' }}>
          {collapsed ? <ExpandMore fontSize="small" /> : <ExpandLess fontSize="small" />}
        </IconButton>
      </Box>

      {/* Body */}
      <Collapse in={!collapsed}>
        <Box sx={{ p: 1.5 }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.75, fontWeight: 600 }}>
            INITIATIVE ORDER
          </Typography>
          {sortedParticipants.map((p, idx) => (
            <Box
              key={p.id}
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 1,
                px: 1,
                py: 0.5,
                borderRadius: 1,
                mb: 0.5,
                bgcolor: p.isCurrentTurn ? 'rgba(255, 152, 0, 0.12)' : 'transparent',
                border: p.isCurrentTurn ? '1px solid rgba(255, 152, 0, 0.3)' : '1px solid transparent',
              }}
            >
              {/* Turn indicator */}
              {p.isCurrentTurn ? (
                <Typography variant="caption" sx={{ fontWeight: 700, color: 'orange' }}>
                  ⏩
                </Typography>
              ) : (
                <Typography variant="caption" color="text.secondary" sx={{ width: 20 }}>
                  {idx + 1}
                </Typography>
              )}

              {/* Name */}
              <Typography
                variant="body2"
                sx={{
                  fontWeight: p.isCurrentTurn ? 700 : 400,
                  flex: 1,
                  color: p.isCurrentTurn ? 'text.primary' : 'text.secondary',
                }}
              >
                {p.displayName}
              </Typography>

              {/* HP */}
              <Typography
                variant="caption"
                sx={{
                  color: hpColor(p.currentHP, p.maxHP),
                  fontWeight: 600,
                  minWidth: 55,
                  textAlign: 'right',
                }}
              >
                HP: {p.currentHP}/{p.maxHP}
              </Typography>

              {/* AC */}
              <Typography variant="caption" color="text.secondary">
                AC: {p.ac}
              </Typography>

              {/* Initiative */}
              <Chip
                label={p.initiative}
                size="small"
                sx={{
                  height: 18,
                  fontSize: 10,
                  minWidth: 28,
                  bgcolor: 'rgba(158, 158, 158, 0.1)',
                  color: 'text.secondary',
                }}
              />
            </Box>
          ))}

          {/* Conditions for current turn */}
          {(() => {
            const current = combat.participants.find(p => p.isCurrentTurn);
            if (!current || current.conditions.length === 0) return null;
            return (
              <Box sx={{ mt: 1, pt: 0.75, borderTop: '1px solid rgba(0,0,0,0.1)' }}>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                  Conditions ({current.displayName}):
                </Typography>
                <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                  {current.conditions.map((c, i) => (
                    <Chip
                      key={i}
                      label={c.duration != null ? `${c.name} (${c.duration})` : c.name}
                      size="small"
                      sx={{
                        height: 18,
                        fontSize: 10,
                        bgcolor: 'rgba(244, 67, 54, 0.1)',
                        color: 'error.main',
                      }}
                    />
                  ))}
                </Box>
              </Box>
            );
          })()}

          {/* Actions */}
          <Box sx={{ display: 'flex', gap: 1, mt: 1.5, pt: 0.75, borderTop: '1px solid rgba(0,0,0,0.1)' }}>
            <Button
              size="small"
              variant="outlined"
              onClick={() => window.location.href = `/game/${gameId}/combat`}
              sx={{
                fontSize: 11,
                height: 28,
                borderColor: 'rgba(0,0,0,0.2)',
              }}
            >
              View Combat Tab →
            </Button>
          </Box>
        </Box>
      </Collapse>
    </Paper>
  );
}

// ==================== Chat Input ====================

function ChatInput({
  inputType,
  setInputType,
  inputTarget,
  setInputTarget,
  whisperTargetPlayer,
  setWhisperTargetPlayer,
  inputValue,
  setInputValue,
  onSend,
  players,
  isCreator,
}: {
  inputType: MessageInputType;
  setInputType: (t: MessageInputType) => void;
  inputTarget: MessageTarget;
  setInputTarget: (t: MessageTarget) => void;
  whisperTargetPlayer: string | null;
  setWhisperTargetPlayer: (t: string | null) => void;
  inputValue: string;
  setInputValue: (v: string) => void;
  onSend: () => void;
  players: Array<{ id: string; characterName: string; status: string }>;
  isCreator: boolean;
}) {
  const activePlayers = players.filter(p => p.status === 'Active');

  const placeholder = (() => {
    if (inputTarget === 'gm') {
      return inputType === 'inGame' ? 'Whisper to the GM...' : 'OOC whisper to GM...';
    }
    if (inputTarget === 'player') {
      return 'Type your whisper...';
    }
    return inputType === 'inGame' ? 'Speak in-character...' : 'Speak out-of-character...';
  })();

  return (
    <Box sx={{ px: 1.5, pb: 1.5 }}>
      {/* Player whisper target chips */}
      {whisperTargetPlayer && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.75 }}>
          <Typography variant="caption" color="text.secondary">
            📩 Whispering to:
          </Typography>
          {(() => {
            const player = activePlayers.find(p => p.id === whisperTargetPlayer);
            return player ? (
              <Chip
                label={player.characterName}
                size="small"
                sx={{ height: 20, fontSize: 11, bgcolor: 'warning.lighter', color: 'warning.dark' }}
              />
            ) : null;
          })()}
          <IconButton size="small" onClick={() => setWhisperTargetPlayer(null)} sx={{ p: 0.25 }}>
            <Typography variant="caption" color="text.secondary">✕</Typography>
          </IconButton>
        </Box>
      )}

      {/* Input row */}
      <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
        {/* Type toggle — single button */}
        <Button
          size="small"
          onClick={() => setInputType(inputType === 'inGame' ? 'ooc' : 'inGame')}
          sx={{
            bgcolor: inputType === 'inGame' ? 'success.lighter' : 'info.lighter',
            color: inputType === 'inGame' ? 'success.dark' : 'info.dark',
            minWidth: 90,
            px: 2,
            fontSize: 12,
            fontWeight: 600,
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 2,
            '&:hover': { bgcolor: inputType === 'inGame' ? 'success.lighter' : 'info.lighter' },
          }}
        >
          {inputType === 'inGame' ? '🎮 In-Game' : '📢 OOC'}
        </Button>

        {/* Receiver dropdown */}
        <FormControl size="small" sx={{ minWidth: 120, flexShrink: 0 }}>
          <InputLabel>Receiver</InputLabel>
          <Select
            value={inputTarget}
            label="Receiver"
            onChange={(e) => setInputTarget(e.target.value as MessageTarget)}
            sx={{ fontSize: 12, height: 36 }}
          >
            <MenuItem value="all">🌐 All (Public)</MenuItem>
            <MenuItem value="gm">🤫 To GM</MenuItem>
            {isCreator && <MenuItem value="player">📩 To Player...</MenuItem>}
          </Select>
        </FormControl>

        {/* Text input */}
        <TextField
          fullWidth
          size="small"
          placeholder={placeholder}
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && !e.shiftKey && onSend()}
          InputProps={{
            endAdornment: (
              <IconButton
                size="small"
                onClick={onSend}
                disabled={!inputValue.trim()}
                sx={{ color: inputValue.trim() ? 'primary.main' : 'text.disabled' }}
              >
                <SendIcon fontSize="small" />
              </IconButton>
            ),
          }}
        />
      </Box>

      {/* Player chips for whisper target */}
      {inputTarget === 'player' && (
        <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mt: 0.75 }}>
          {activePlayers.map((p) => (
            <Chip
              key={p.id}
              label={p.characterName}
              size="small"
              clickable
              onClick={() => setWhisperTargetPlayer(p.id)}
              sx={{
                height: 22,
                fontSize: 11,
                bgcolor: whisperTargetPlayer === p.id ? 'warning.lighter' : 'background.default',
                color: whisperTargetPlayer === p.id ? 'warning.dark' : 'text.secondary',
              }}
            />
          ))}
        </Box>
      )}
    </Box>
  );
}

// ==================== Main Page ====================

export default function GameChatPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { game, isLoading: isLoadingGame } = useGame(id);
  const { pendingCalls: calls } = useToolCalls(id);
  const { players } = usePlayers(id);
  const { messages, isLoading, hasMore, loadOldest } = useMessagesInfiniteScroll(id, game?.sessionId);
  const { isConnected, on, off, invoke } = useGameHub();
  const chatContainerRef = useRef<HTMLDivElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Input state
  const [messageInput, setMessageInput] = useState('');
  const [messageType, setMessageType] = useState<MessageInputType>('inGame');
  const [inputTarget, setInputTarget] = useState<MessageTarget>('all');
  const [whisperTargetPlayer, setWhisperTargetPlayer] = useState<string | null>(null);

  // Combat state
  const [activeCombat, setActiveCombat] = useState<ActiveCombat | null>(null);

  // New messages indicator
  const [newMessagesCount, setNewMessagesCount] = useState(0);
  const [isAtBottom, setIsAtBottom] = useState(true);

  // Live incoming messages (from SignalR)
  const [liveMessages, setLiveMessages] = useState<any[]>([]);

  // Fetch active combat
  const fetchActiveCombat = useCallback(async () => {
    if (!id) return;
    try {
      const data = await combatGetCombats(id);
      const active = data.combats.find((c: any) => c.status === 'Active' || c.status === 'Paused');
      if (active) {
        // Fetch full combat details
        const { combatGetCombat } = await import('../api/combat/combatApi');
        const full = await combatGetCombat(id, active.id);
        setActiveCombat({
          combatId: full.combatId,
          name: full.name || 'Unnamed Combat',
          status: full.status,
          currentRound: full.currentRound,
          currentTurnIndex: full.currentTurnIndex,
          participants: full.participants.map((p: any) => ({
            id: p.id,
            displayName: p.displayName,
            participantType: p.participantType,
            currentHP: p.currentHP,
            maxHP: p.maxHP,
            ac: p.ac,
            initiative: p.initiative,
            conditions: (p.conditions || []).map((c: any) => ({
              name: c.name || c,
              duration: c.duration,
            })),
            isCurrentTurn: p.isCurrentTurn,
          })),
        });
      } else {
        setActiveCombat(null);
      }
    } catch {
      setActiveCombat(null);
    }
  }, [id]);

  // Fetch combat on mount and periodically
  useEffect(() => {
    fetchActiveCombat();
    const interval = setInterval(fetchActiveCombat, 5000);
    return () => clearInterval(interval);
  }, [fetchActiveCombat]);

  // SignalR: listen for combat events and new messages
  useEffect(() => {
    if (!isConnected) return;

    const handleCombatStarted = () => { fetchActiveCombat(); };
    const handleCombatEnded = () => { setActiveCombat(null); };
    const handleCombatDamage = () => { fetchActiveCombat(); };
    const handleConditionApplied = () => { fetchActiveCombat(); };
    const handleConditionRemoved = () => { fetchActiveCombat(); };
    const handleTurnAdvanced = () => { fetchActiveCombat(); };
    const handleParticipantAdded = () => { fetchActiveCombat(); };
    const handleParticipantRemoved = () => { fetchActiveCombat(); };
    const handleNewMessage = (msg: any) => {
      // Append incoming message to live messages
      setLiveMessages(prev => {
        // Avoid duplicates
        if (prev.some((m: any) => m.id === msg.id)) return prev;
        return [...prev, msg];
      });
    };

    on('CombatStarted', handleCombatStarted);
    on('CombatEnded', handleCombatEnded);
    on('CombatDamageDealt', handleCombatDamage);
    on('CombatConditionApplied', handleConditionApplied);
    on('CombatConditionRemoved', handleConditionRemoved);
    on('TurnAdvanced', handleTurnAdvanced);
    on('ParticipantAdded', handleParticipantAdded);
    on('ParticipantRemoved', handleParticipantRemoved);
    on('NewMessage', handleNewMessage);

    return () => {
      off('CombatStarted', handleCombatStarted);
      off('CombatEnded', handleCombatEnded);
      off('CombatDamageDealt', handleCombatDamage);
      off('CombatConditionApplied', handleConditionApplied);
      off('CombatConditionRemoved', handleConditionRemoved);
      off('TurnAdvanced', handleTurnAdvanced);
      off('ParticipantAdded', handleParticipantAdded);
      off('ParticipantRemoved', handleParticipantRemoved);
      off('NewMessage', handleNewMessage);
    };
  }, [isConnected, on, off, fetchActiveCombat]);

  // Scroll detection: check if user is at bottom
  const checkAtBottom = useCallback(() => {
    const el = chatContainerRef.current;
    if (!el) return;
    const threshold = 80;
    setIsAtBottom(el.scrollHeight - el.scrollTop - el.clientHeight < threshold);
  }, []);

  // Merge paginated messages with live incoming messages
  const allMessages = useMemo(() => {
    if (liveMessages.length === 0) return messages;
    // Deduplicate: live messages that already exist in paginated are removed
    const liveIds = new Set(liveMessages.map((m: any) => m.id));
    const filteredLive = liveMessages.filter((m: any) => !liveIds.has(m.id) || !messages.some((pm: any) => pm.id === m.id));
    // Append live messages at the end (they're newest)
    return [...messages, ...filteredLive];
  }, [messages, liveMessages]);

  // Auto-scroll to bottom on mount and new messages (when at bottom)
  useEffect(() => {
    if (isAtBottom) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    } else if (allMessages.length > 0) {
      setNewMessagesCount(prev => prev + 1);
    }
  }, [allMessages.length, isAtBottom]);

  // Scroll listener
  useEffect(() => {
    const el = chatContainerRef.current;
    if (!el) return;
    el.addEventListener('scroll', checkAtBottom, { passive: true });
    return () => el.removeEventListener('scroll', checkAtBottom);
  }, [checkAtBottom]);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    setNewMessagesCount(0);
  }, []);

  const sendMessage = useCallback(async () => {
    if (!messageInput.trim() || !id) return;

    // Resolve target player ID for GM→player whispers
    let targetPlayerId: string | undefined;
    if (inputTarget === 'player' && whisperTargetPlayer) {
      targetPlayerId = whisperTargetPlayer;
    }

    // Determine hub message type
    let hubMessageType: string;
    if (messageType === 'ooc') {
      hubMessageType = inputTarget === 'gm' ? 'oocWhisper' : 'ooc';
    } else {
      hubMessageType = inputTarget === 'gm' ? 'inGameWhisper' : 'inGame';
    }

    try {
      await invoke(hubMessageType, messageInput, targetPlayerId);
      setMessageInput('');
      setNewMessagesCount(0);
    } catch (err) {
      // Error already sent via SignalR
    }
  }, [messageInput, id, messageType, inputTarget, whisperTargetPlayer, invoke]);

  const handleLeaveGame = useCallback(async () => {
    if (!id) return;
    await api.leaveGame(id);
    navigate('/');
  }, [id, navigate]);

  void handleLeaveGame;

  if (isLoadingGame) return <Typography>Loading...</Typography>;
  if (!game) return <Typography>No game data available.</Typography>;

  return (
    <Box sx={{ display: 'flex', height: '100vh', bgcolor: 'background.default' }}>
      <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        {/* Zone A: Collapsible Combat Panel */}
        {activeCombat && (
          <CollapsibleCombatPanel combat={activeCombat} gameId={id!} />
        )}

        {/* Zone B: Message Feed */}
        <Box
          sx={{
            flex: 1,
            overflow: 'hidden',
            display: 'flex',
            flexDirection: 'column',
            bgcolor: 'background.paper',
            borderRadius: 2,
            mx: 1.5,
            mb: 0,
          }}
          ref={chatContainerRef}
        >
          <ChatPanel
            messages={allMessages}
            isLoadingMore={isLoading}
            hasMore={hasMore}
            loadMoreOldest={loadOldest}
            messagesEndRef={messagesEndRef}
            newMessagesCount={newMessagesCount}
            onScrollToBottom={scrollToBottom}
          />
        </Box>

        {/* Zone C: Single-Line Input */}
        <Box sx={{ px: 1.5, py: 1, bgcolor: 'background.paper' }}>
          <ChatInput
            inputType={messageType}
            setInputType={setMessageType}
            inputTarget={inputTarget}
            setInputTarget={setInputTarget}
            whisperTargetPlayer={whisperTargetPlayer}
            setWhisperTargetPlayer={setWhisperTargetPlayer}
            inputValue={messageInput}
            setInputValue={setMessageInput}
            onSend={sendMessage}

            players={players}
            isCreator={user?.role === 'Creator'}
          />
        </Box>
      </Box>

      {/* Tool Call Banner */}
      <ToolCallBanner
        pendingCalls={calls || []}
        onConfirm={async (_callId, _approved) => {}}
        onRoll={async (_callId) => {}}
        onDecline={async (_callId) => {}}
        onDismiss={(_callId) => {}}
        isCreator={user?.role === 'Creator'}
      />
    </Box>
  );
}
