import { useEffect, useRef, useState, useCallback } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, TextField, Button, IconButton, Chip, CircularProgress,
  Select, MenuItem, FormControl, Paper, Divider, Alert, Collapse,
  Tooltip, LinearProgress, Badge,
} from '@mui/material'
import {
  Send as SendIcon, ExpandLess, ExpandMore, Shield as ShieldIcon,
  Favorite as HPIcon, Warning as DeathIcon, SmartToy as GMIcon,
} from '@mui/icons-material'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { useGame } from '../api/hooks/useGame'
import { usePlayers } from '../api/hooks/usePlayers'
import { useGameHub } from '../api/hooks/useHub'
import { useMessagesInfiniteScroll } from '../api/hooks/useMessagesInfiniteScroll'
import { useToolCalls } from '../api/hooks/useToolCalls'
import { useAuth } from '../context/AuthContext'
import type { Message, Combat, CombatParticipant, Player } from '../types'

type ReceiverType = 'All' | 'GM' | string

function MsgBubble({ msg, players }: { msg: Message; players: Player[] }) {
  const isGM = msg.type === 'GM'
  const isSystem = ['System', 'PlayerJoined', 'PlayerLeft', 'GameStarted', 'SessionCreated'].includes(msg.type)
  const isDice = ['DiceRoll', 'SkillCheck', 'AttackRoll'].includes(msg.type)
  const isWhisper = msg.type === 'Whisper' || msg.type === 'OOCWhisper'
  const isOOC = msg.isOOC || msg.type === 'OOC'

  const playerName = msg.playerDisplayName
    ?? players.find(p => p.id === msg.playerId)?.displayName
    ?? (msg.playerId ? 'Player' : null)

  if (isSystem) {
    return (
      <Box sx={{ textAlign: 'center', py: 0.5 }}>
        <Typography variant="caption" color="text.secondary" fontStyle="italic">{msg.content}</Typography>
      </Box>
    )
  }

  return (
    <Box sx={{ mb: 1, maxWidth: isGM ? '100%' : '80%', alignSelf: isGM ? 'stretch' : 'flex-start' }}>
      {playerName && !isGM && (
        <Typography variant="caption" color={isWhisper ? 'secondary' : 'text.secondary'} sx={{ ml: 1, mb: 0.25, display: 'block' }}>
          {isWhisper ? `🔒 ${playerName}` : isOOC ? `[OOC] ${playerName}` : playerName}
        </Typography>
      )}
      <Paper
        elevation={0}
        sx={{
          p: isGM ? 1.5 : 1,
          bgcolor: isGM
            ? 'rgba(124, 58, 237, 0.08)'
            : isDice
            ? 'rgba(16, 185, 129, 0.08)'
            : isWhisper
            ? 'rgba(245, 158, 11, 0.08)'
            : isOOC
            ? 'action.hover'
            : 'background.paper',
          border: '1px solid',
          borderColor: isGM
            ? 'rgba(124, 58, 237, 0.2)'
            : isDice
            ? 'rgba(16, 185, 129, 0.2)'
            : isWhisper
            ? 'rgba(245, 158, 11, 0.2)'
            : 'divider',
          borderRadius: 2,
        }}
      >
        {isGM && (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 1 }}>
            <GMIcon sx={{ fontSize: 14, color: 'primary.main' }} />
            <Typography variant="caption" color="primary" fontWeight={600}>Game Master</Typography>
          </Box>
        )}
        {isGM ? (
          <Box sx={{ '& p': { m: 0, mb: 1 }, '& p:last-child': { mb: 0 }, '& h1,& h2,& h3': { color: 'primary.main' } }}>
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{msg.content}</ReactMarkdown>
          </Box>
        ) : (
          <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>{msg.content}</Typography>
        )}
        {isDice && msg.metadata && (
          <Box sx={{ mt: 0.5 }}>
            <Typography variant="caption" color="success.main" fontWeight={600}>
              🎲 {JSON.stringify(msg.metadata)}
            </Typography>
          </Box>
        )}
      </Paper>
      <Typography variant="caption" color="text.disabled" sx={{ ml: 1, display: 'block' }}>
        {new Date(msg.createdAt).toLocaleTimeString()}
      </Typography>
    </Box>
  )
}

function HPBar({ hp, maxHP }: { hp: number; maxHP: number }) {
  const pct = maxHP > 0 ? Math.max(0, Math.min(100, (hp / maxHP) * 100)) : 0
  const color = pct > 50 ? 'success' : pct > 25 ? 'warning' : 'error'
  return (
    <Box sx={{ flex: 1 }}>
      <LinearProgress variant="determinate" value={pct} color={color} sx={{ height: 6, borderRadius: 3 }} />
      <Typography variant="caption" color="text.secondary">{hp}/{maxHP}</Typography>
    </Box>
  )
}

function CombatPanel({ combat }: { combat: Combat }) {
  const [open, setOpen] = useState(true)
  const current = combat.participants[combat.currentTurnIndex]

  return (
    <Paper elevation={2} sx={{ mb: 1, overflow: 'hidden' }}>
      <Box
        sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 2, py: 1, cursor: 'pointer', bgcolor: 'rgba(124,58,237,0.1)' }}
        onClick={() => setOpen(o => !o)}
      >
        <Typography variant="subtitle2" fontWeight={600} color="primary">
          ⚔️ Combat — Round {combat.currentRound}
        </Typography>
        {open ? <ExpandLess fontSize="small" /> : <ExpandMore fontSize="small" />}
      </Box>
      <Collapse in={open}>
        <Box sx={{ p: 1, display: 'flex', flexDirection: 'column', gap: 0.5, maxHeight: 250, overflowY: 'auto' }}>
          {combat.participants.map((p, i) => (
            <Box
              key={p.id}
              sx={{
                display: 'flex', alignItems: 'center', gap: 1, p: 0.75,
                borderRadius: 1,
                bgcolor: i === combat.currentTurnIndex ? 'rgba(124,58,237,0.15)' : 'transparent',
                border: i === combat.currentTurnIndex ? '1px solid rgba(124,58,237,0.4)' : '1px solid transparent',
              }}
            >
              <Typography variant="caption" color="text.secondary" sx={{ width: 24, textAlign: 'center' }}>
                {p.initiative}
              </Typography>
              <Typography variant="body2" sx={{ flex: 1 }} fontWeight={i === combat.currentTurnIndex ? 600 : 400}>
                {p.displayName}
                {p.conditions?.length > 0 && (
                  <Typography component="span" variant="caption" color="warning.main" sx={{ ml: 0.5 }}>
                    ({p.conditions.join(', ')})
                  </Typography>
                )}
              </Typography>
              <HPBar hp={p.hp} maxHP={p.maxHP} />
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.25 }}>
                <ShieldIcon sx={{ fontSize: 12, color: 'text.secondary' }} />
                <Typography variant="caption">{p.ac}</Typography>
              </Box>
              {p.deathSaveState && !p.deathSaveState.isStable && !p.deathSaveState.isDead && (
                <Tooltip title={`Death saves: ${p.deathSaveState.successes}✓ ${p.deathSaveState.failures}✗`}>
                  <Badge badgeContent={p.deathSaveState.failures} color="error">
                    <DeathIcon sx={{ fontSize: 16, color: 'error.main' }} />
                  </Badge>
                </Tooltip>
              )}
            </Box>
          ))}
        </Box>
        {current && (
          <Box sx={{ px: 2, py: 1, bgcolor: 'action.hover', display: 'flex', gap: 1 }}>
            <Chip label={`${current.actionsRemaining} action`} size="small" />
            <Chip label={`${current.bonusActionsRemaining} bonus`} size="small" variant="outlined" />
            <Chip label={`${current.movementsRemaining}ft`} size="small" variant="outlined" />
          </Box>
        )}
      </Collapse>
    </Paper>
  )
}

function ToolCallBanner({ gameId }: { gameId: string }) {
  const { toolCalls, confirm } = useToolCalls(gameId)
  if (!toolCalls.length) return null
  return (
    <Alert severity="info" sx={{ mb: 1 }} action={
      <Button size="small" onClick={() => confirm(toolCalls[0].id)}>Confirm</Button>
    }>
      AI is waiting: <strong>{toolCalls[0].name}</strong>
    </Alert>
  )
}

export default function GameChatPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const { user } = useAuth()
  const { game } = useGame(gameId ?? null)
  const { players } = usePlayers(gameId ?? null)
  const hub = useGameHub()
  const { messages, loading: msgsLoading, hasMore, loadInitial, loadOlder, appendLive } = useMessagesInfiniteScroll(game?.currentSessionId ?? null)
  const [combat, setCombat] = useState<Combat | null>(null)
  const [input, setInput] = useState('')
  const [isOOC, setIsOOC] = useState(false)
  const [receiver, setReceiver] = useState<ReceiverType>('All')
  const [gmThinking, setGmThinking] = useState(false)
  const [gmError, setGmError] = useState<string | null>(null)
  const bottomRef = useRef<HTMLDivElement>(null)
  const chatRef = useRef<HTMLDivElement>(null)
  const isAtBottom = useRef(true)

  useEffect(() => {
    if (!gameId) return
    hub.connect(gameId).then(() => {
      loadInitial()
    })
    return () => { hub.disconnect() }
  }, [gameId]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const handleNewMessage = (msg: Message) => {
      appendLive(msg)
      if (isAtBottom.current) setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: 'smooth' }), 50)
    }
    const handleGMThinking = () => setGmThinking(true)
    const handleGMDone = () => setGmThinking(false)
    const handleGMError = ({ message }: { message: string }) => setGmError(message)
    const handleCombatStarted = (c: Combat) => setCombat(c)
    const handleCombatEnded = () => setCombat(null)
    const handleTurnAdvanced = (c: Combat) => setCombat(c)

    hub.on('NewMessage', handleNewMessage)
    hub.on('GMThinking', handleGMThinking)
    hub.on('GMDoneThinking', handleGMDone)
    hub.on('GMError', handleGMError)
    hub.on('CombatStarted', handleCombatStarted)
    hub.on('CombatEnded', handleCombatEnded)
    hub.on('TurnAdvanced', handleTurnAdvanced)
    const handleCombatUpdate = (c: Combat) => setCombat(c)
    hub.on('CombatDamageDealt', handleCombatUpdate)
    hub.on('CombatConditionApplied', handleCombatUpdate)
    hub.on('CombatConditionRemoved', handleCombatUpdate)

    return () => {
      hub.off('NewMessage', handleNewMessage)
      hub.off('GMThinking', handleGMThinking)
      hub.off('GMDoneThinking', handleGMDone)
      hub.off('GMError', handleGMError)
      hub.off('CombatStarted', handleCombatStarted)
      hub.off('CombatEnded', handleCombatEnded)
      hub.off('TurnAdvanced', handleTurnAdvanced)
      hub.off('CombatDamageDealt', handleCombatUpdate)
      hub.off('CombatConditionApplied', handleCombatUpdate)
      hub.off('CombatConditionRemoved', handleCombatUpdate)
    }
  }, [hub, appendLive])

  useEffect(() => {
    if (isAtBottom.current) bottomRef.current?.scrollIntoView()
  }, [messages])

  const handleScroll = useCallback(() => {
    const el = chatRef.current
    if (!el) return
    isAtBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80
    if (el.scrollTop < 80 && hasMore && !msgsLoading) loadOlder()
  }, [hasMore, msgsLoading, loadOlder])

  const send = async () => {
    if (!input.trim() || !gameId || !game?.currentSessionId) return
    const content = input.trim()
    setInput('')

    try {
      if (receiver === 'GM') {
        await hub.invoke('SendWhisper', gameId, game.currentSessionId, content, null)
      } else if (receiver !== 'All') {
        await hub.invoke('SendWhisper', gameId, game.currentSessionId, content, receiver)
      } else if (isOOC) {
        await hub.invoke('SendOOCMessage', gameId, game.currentSessionId, content)
      } else {
        await hub.invoke('SendMessage', gameId, game.currentSessionId, content, false, 'All')
      }
    } catch (e) {
      setInput(content)
    }
  }

  const otherPlayers = players.filter(p => p.userId !== user?.id)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100vh', overflow: 'hidden' }}>
      {/* Zone A: Combat panel */}
      {combat && (
        <Box sx={{ px: 2, pt: 1 }}>
          <CombatPanel combat={combat} />
        </Box>
      )}

      {/* Tool call banner */}
      {gameId && (
        <Box sx={{ px: 2 }}>
          <ToolCallBanner gameId={gameId} />
        </Box>
      )}

      {/* GM Error */}
      {gmError && (
        <Alert severity="error" onClose={() => setGmError(null)} sx={{ mx: 2 }}>
          {gmError}
        </Alert>
      )}

      {/* Zone B: Chat */}
      <Box
        ref={chatRef}
        onScroll={handleScroll}
        sx={{ flex: 1, overflowY: 'auto', px: 2, py: 1, display: 'flex', flexDirection: 'column', gap: 0.5 }}
      >
        {hasMore && (
          <Box sx={{ textAlign: 'center', py: 1 }}>
            {msgsLoading ? <CircularProgress size={20} /> : (
              <Button size="small" onClick={loadOlder}>Load older messages</Button>
            )}
          </Box>
        )}
        {messages.map(msg => (
          <MsgBubble key={msg.id} msg={msg} players={players} />
        ))}
        {gmThinking && (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, opacity: 0.7 }}>
            <GMIcon sx={{ fontSize: 14, color: 'primary.main' }} />
            <Typography variant="caption" color="primary">GM is composing…</Typography>
            <CircularProgress size={12} color="inherit" />
          </Box>
        )}
        <div ref={bottomRef} />
      </Box>

      <Divider />

      {/* Zone C: Input */}
      <Box sx={{ p: 1.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
          <Button
            size="small"
            variant={isOOC ? 'contained' : 'outlined'}
            onClick={() => setIsOOC(o => !o)}
            sx={{ flexShrink: 0, fontSize: 11, px: 1 }}
            color={isOOC ? 'secondary' : 'inherit'}
          >
            OOC
          </Button>
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <Select value={receiver} onChange={e => setReceiver(e.target.value as ReceiverType)}>
              <MenuItem value="All">To All</MenuItem>
              <MenuItem value="GM">To GM</MenuItem>
              {otherPlayers.map(p => (
                <MenuItem key={p.id} value={p.id}>To {p.displayName ?? p.characterName ?? 'Player'}</MenuItem>
              ))}
            </Select>
          </FormControl>
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            fullWidth size="small" placeholder="Say something…"
            value={input} onChange={e => setInput(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send() } }}
            multiline maxRows={4}
          />
          <IconButton color="primary" onClick={send} disabled={!input.trim()}>
            <SendIcon />
          </IconButton>
        </Box>
        <Box sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
          <Typography variant="caption" color={hub.isConnected ? 'success.main' : 'error.main'}>
            {hub.isConnected ? '● Connected' : '○ Disconnected'}
          </Typography>
          {game?.gmStatus === 'Running' && (
            <Chip label="GM Active" size="small" color="primary" sx={{ ml: 1 }} />
          )}
          {game?.gmStatus === 'Paused' && (
            <Chip label="GM Paused" size="small" variant="outlined" sx={{ ml: 1 }} />
          )}
        </Box>
      </Box>
    </Box>
  )
}
