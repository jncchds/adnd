import { useState, useCallback, useRef } from 'react';
import { Box, Typography, Paper, IconButton, Collapse, Chip, InputAdornment, Button, MenuItem, Select, FormControl, TextField, InputLabel } from '@mui/material';
import { Send as SendIcon } from '@mui/icons-material';
import MessageBubble from './MessageBubble';
import type { MessagePaginated } from '../../types';
import type { UnifiedMessage } from '../../api/hooks/useMessages';

type MessageInputType = 'inGame' | 'ooc';
type MessageTarget = 'all' | 'gm' | 'player';

interface UnifiedChatPanelProps {
  messages: MessagePaginated[];
  isLoadingMore: boolean;
  hasMore: boolean;
  loadMoreOldest: () => void;
  inputType?: MessageInputType;
  setInputType?: (t: MessageInputType) => void;
  inputTarget?: MessageTarget;
  setInputTarget?: (t: MessageTarget) => void;
  whisperTargetPlayer?: string | null;
  setWhisperTargetPlayer?: (t: string | null) => void;
  _whisperInput?: string;
  setWhisperInput?: (t: string) => void;
  onSend?: () => Promise<void>;
  onDiceRoll?: () => void;
  onSkillCheck?: (skill: string, dc: number) => void;
  showDiceHistory?: boolean;
  setShowDiceHistory?: (v: boolean) => void;
  messagesEndRef?: React.RefObject<HTMLDivElement | null>;
  players?: any[];
  isConnected?: boolean;
  activeSession?: any;
  isCreator?: boolean;
  isMobile?: boolean;
  onOpenCharacter?: () => void;
}

export default function ChatPanel({
  messages, isLoadingMore, hasMore, loadMoreOldest,
  inputType, setInputType, inputTarget, setInputTarget,
  whisperTargetPlayer, setWhisperTargetPlayer, _whisperInput, setWhisperInput,
  onSend, onDiceRoll, onSkillCheck, showDiceHistory: _showDiceHistory, setShowDiceHistory: _setShowDiceHistory,
  messagesEndRef, players, isConnected: _isConnected, activeSession, isCreator, isMobile,
  onOpenCharacter
}: UnifiedChatPanelProps) {
  const [quickSkill, setQuickSkill] = useState('Perception');
  const [quickDC, setQuickDC] = useState(15);
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  void (messagesEndRef || { current: null });

  // Default no-op functions for optional callbacks
  const _onSend = onSend ?? (() => Promise.resolve());
  const _onSkillCheck = onSkillCheck ?? ((_skill: string, _dc: number) => {});
  const _setInputType = setInputType ?? (() => {});
  const _setInputTarget = setInputTarget ?? (() => {});
  const _setWhisperTargetPlayer = setWhisperTargetPlayer ?? (() => {});
  const _setWhisperInput = setWhisperInput ?? (() => {});
  const _whisperInputVal = _whisperInput ?? '';
  const _players = players ?? [];

  // Convert MessagePaginated to UnifiedMessage for MessageBubble
  const toUnifiedMessage = (msg: MessagePaginated): UnifiedMessage => ({
    id: msg.id,
    type: msg.isOOC ? 'oocPublic' : 'inGamePublic',
    content: msg.content,
    senderName: msg.playerName,
    senderRole: '',
    timestamp: msg.createdAt,
    isSystem: msg.type === 0,
    isWhisper: false,
    diceFormula: msg.metadata?.diceFormula,
    diceTotal: msg.metadata?.diceTotal,
    skill: msg.metadata?.skill,
    skillDC: msg.metadata?.skillDC,
  });

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
    _onSend();
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
            onClick={() => _setInputType('inGame')}
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
            onClick={() => _setInputType('ooc')}
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
            onChange={e => _setInputTarget(e.target.value as MessageTarget)}
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
            onClick={() => _onSkillCheck(quickSkill, quickDC)}
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
          {_players.filter((p: any) => p.status === 'Active').map((p: any) => (
            <Chip
              key={p.id}
              label={p.characterName}
              size="small"
              clickable
              onClick={() => _setWhisperTargetPlayer(p.id)}
              sx={{ m: 0.25 }}
            />
          ))}
        </Box>
      )}

      {/* Dice History */}
      <Collapse in={_showDiceHistory}>
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
            .filter(m => m.type === 5)
            .slice(-10)
            .reverse()
            .map((m, i) => {
              const meta = m.metadata || {};
              return (
                <Typography key={i} variant="caption" sx={{ display: 'block' }}>
                  {(meta.diceFormula as string) || 'roll'}: {(meta.diceTotal as number) || '?'} [{(meta.diceRolls as number[])?.join(',')}]{(meta.diceRolls as number[]) ? '' : ''} — {m.playerName}
                </Typography>
              );
            })}
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
            <MessageBubble key={msg.id} msg={toUnifiedMessage(msg)} />
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
              📩 Whispering to: {_players.find(p => p.id === whisperTargetPlayer)?.characterName}
            </Typography>
            <Button size="small" onClick={() => _setWhisperTargetPlayer(null)}>Clear</Button>
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
            value={_whisperInputVal}
            onChange={e => _setWhisperInput(e.target.value)}
            onKeyDown={handleKeyDown}
            multiline
            maxRows={4}
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={handleSend} size="small" disabled={!_whisperInputVal.trim()}>
                    <SendIcon />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
          <Button
            variant="contained"
            onClick={handleSend}
            disabled={!_whisperInputVal.trim()}
            sx={{ minWidth: isMobile ? 60 : 80 }}
          >
            Send
          </Button>
        </Box>
      </Box>
    </Paper>
  );
}

