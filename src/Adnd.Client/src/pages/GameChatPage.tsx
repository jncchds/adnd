import { useEffect, useRef, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  Box, Typography, TextField, Button, IconButton, Chip, CircularProgress,
  Select, MenuItem, FormControl, Paper, Divider, Alert, Collapse,
  Tooltip, LinearProgress, Badge,
} from '@mui/material'
import {
  Send as SendIcon, ExpandLess, ExpandMore, Shield as ShieldIcon,
  Warning as DeathIcon, SmartToy as GMIcon, Person as CharIcon,
} from '@mui/icons-material'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { useGame } from '../api/hooks/useGame'
import { usePlayers } from '../api/hooks/usePlayers'
import { useGameHub } from '../api/hooks/useHub'
import { useMessagesInfiniteScroll } from '../api/hooks/useMessagesInfiniteScroll'
import { useAuth } from '../context/AuthContext'
import { api } from '../api/client'
import type {
  Message, Combat, Player, Character,
  DamageEvent, HealEvent, ConditionEvent, GMActivity, RerollOption,
} from '../types'
import { activityLabel } from '../utils/gmActivity'

type ReceiverType = 'All' | 'GM' | string

function MsgBubble({ msg, players, characters, prompt }: {
  msg: Message
  players: Player[]
  characters: Character[]
  prompt: React.ReactNode
}) {
  const isGM = msg.type === 'GM'
  const isSystem = ['System', 'PlayerJoined', 'PlayerLeft', 'GameStarted', 'SessionCreated'].includes(msg.type)
  const isDice = ['DiceRoll', 'SkillCheck', 'AttackRoll'].includes(msg.type)
  // A prompt is addressed to one player, so it renders like a whisper — visually marked as
  // "only you can see this", which is exactly what it is.
  const isPrompt = ['RollRequest', 'RerollOffer', 'RollDecline'].includes(msg.type)
  const isWhisper = msg.type === 'Whisper' || msg.type === 'OOCWhisper' || isPrompt
  const isOOC = msg.isOOC || msg.type === 'OOC'
  // History sends isSecret as a field; the live hub carries it in the roll metadata.
  const isSecret = msg.isSecret === true || (msg.metadata as { isSecret?: boolean } | null)?.isSecret === true

  // Sender label leads with the character name (what the table sees in-fiction), with the
  // account's display name in brackets — previously this showed the raw account name only,
  // so "Bob" narrating as "Thorin Ironfist" appeared in chat as just "Bob". The full Character
  // sheet's name takes priority over Player.characterName (a quick label chosen at join time
  // that never syncs once "Create My Character" produces the real sheet).
  const senderPlayer = players.find(p => p.id === msg.playerId)
  const characterName = characters.find(c => c.playerId === msg.playerId)?.name?.trim()
    || senderPlayer?.characterName?.trim()
    || null
  const accountName = senderPlayer?.displayName ?? msg.playerDisplayName ?? null
  const playerName = characterName
    ? (accountName && accountName !== characterName ? `${characterName} (${accountName})` : characterName)
    : accountName
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
          <Typography
            variant="body2"
            color={isDice ? 'success.main' : undefined}
            fontWeight={isDice ? 600 : undefined}
            sx={{ whiteSpace: 'pre-wrap' }}
          >
            {/* Marked so the roller knows the rest of the table cannot see this one. */}
            {isDice && '🎲 '}{isSecret && '🔒 '}{msg.content}
          </Typography>
        )}
        {prompt}
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

/**
 * The buttons on a prompt message. A prompt is a real row in the log rather than a banner
 * over it, so this renders inside the bubble and the answer replaces the row in place —
 * the ask stays where it was asked instead of vanishing.
 */
function PromptActions({ msg, onRoll, onDecline, onReroll }: {
  msg: Message
  onRoll: (toolCallId: string) => void
  onDecline: (toolCallId: string) => void
  onReroll: (toolCallId: string | null, featureId: string | null) => void
}) {
  const [busy, setBusy] = useState(false)
  const meta = (msg.metadata ?? {}) as {
    kind?: string
    toolCallId?: string | null
    options?: RerollOption[]
  }

  const run = async (fn: () => void | Promise<void>) => {
    setBusy(true)
    try { await fn() } finally { setBusy(false) }
  }

  if (meta.kind === 'rollRequest' && meta.toolCallId) {
    const id = meta.toolCallId
    return (
      <Box sx={{ display: 'flex', gap: 0.5, mt: 1 }}>
        <Button size="small" variant="contained" disabled={busy} onClick={() => run(() => onRoll(id))}>Roll</Button>
        <Button size="small" color="inherit" disabled={busy} onClick={() => run(() => onDecline(id))}>Decline</Button>
      </Box>
    )
  }

  if (meta.kind === 'rerollOffer') {
    return (
      <Box sx={{ display: 'flex', gap: 0.5, mt: 1, flexWrap: 'wrap' }}>
        {(meta.options ?? []).map(o => (
          <Tooltip key={o.featureId} title={o.description}>
            <span>
              <Button size="small" variant="outlined" disabled={busy}
                onClick={() => run(() => onReroll(meta.toolCallId ?? null, o.featureId))}>
                {o.name}{o.usesRemaining != null ? ` (${o.usesRemaining} left)` : ''}
              </Button>
            </span>
          </Tooltip>
        ))}
        <Button size="small" color="inherit" disabled={busy}
          onClick={() => run(() => onReroll(meta.toolCallId ?? null, null))}>
          Keep the roll
        </Button>
      </Box>
    )
  }

  return null
}

export default function GameChatPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const { user } = useAuth()
  const navigate = useNavigate()
  const { game, loading: gameLoading } = useGame(gameId ?? null)
  const { players } = usePlayers(gameId ?? null)
  const [myCharacter, setMyCharacter] = useState<Character | null | undefined>(undefined)
  const [characters, setCharacters] = useState<Character[]>([])

  useEffect(() => {
    if (!gameId || gameLoading) return
    // The Creator can also play a character (they're a table admin, not the GM — see
    // CLAUDE.md), so they need the same character check as any other player.
    api.characters.getMy(gameId)
      .then(c => setMyCharacter(c ?? null))
      .catch(() => setMyCharacter(null))
    // Whole-party roster so chat can label senders by their character's actual name.
    api.characters.listForGame(gameId)
      .then(setCharacters)
      .catch(() => setCharacters([]))
  }, [gameId, gameLoading])
  const hub = useGameHub()
  const { messages, loading: msgsLoading, hasMore, loadInitial, loadOlder, appendLive, replaceLive, removeLive } = useMessagesInfiniteScroll(game?.currentSessionId ?? null)
  const [combat, setCombat] = useState<Combat | null>(null)
  const [input, setInput] = useState('')
  const [isOOC, setIsOOC] = useState(false)
  const [receiver, setReceiver] = useState<ReceiverType>('All')
  const [gmActivity, setGmActivity] = useState<GMActivity | null>(null)
  const [gmError, setGmError] = useState<string | null>(null)
  const [sendError, setSendError] = useState<string | null>(null)
  const bottomRef = useRef<HTMLDivElement>(null)
  const chatRef = useRef<HTMLDivElement>(null)
  const isAtBottom = useRef(true)

  useEffect(() => {
    if (!gameId) return
    hub.connect(gameId).catch(() => { /* surfaced via hub.error */ })
    return () => { hub.disconnect().catch(() => { /* unmounting anyway */ }) }
  }, [gameId]) // eslint-disable-line react-hooks/exhaustive-deps

  // Keyed on the session, not the game. loadInitial used to be called with the closure
  // captured at mount, when currentSessionId was still null, so it always no-opped and
  // nothing ever re-triggered it — the chat log stayed permanently empty.
  useEffect(() => {
    if (game?.currentSessionId) loadInitial()
  }, [game?.currentSessionId]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const handleNewMessage = (msg: Message) => {
      appendLive(msg)
      // A GM message arriving is the definitive end of a turn — clears the activity
      // indicator even if a Completed/Failed broadcast was somehow missed.
      if (msg.type === 'GM') setGmActivity(null)
      if (isAtBottom.current) setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: 'smooth' }), 50)
    }
    const handleGMActivity = (a: GMActivity) => setGmActivity(a.step === 'Completed' || a.step === 'Failed' ? null : a)
    // A prompt resolving into its outcome, in place. Sent to the whole group when the
    // outcome is public and to the one player when it stays private.
    const handleMessageUpdated = (m: Message) => replaceLive(m)
    const handleMessageRemoved = ({ messageId }: { messageId: string }) => removeLive(messageId)
    const handleGMError = ({ message }: { message: string }) => { setGmError(message); setGmActivity(null) }
    const handleCombatStarted = (c: Combat) => setCombat(c)
    const handleCombatEnded = () => setCombat(null)
    const handleTurnAdvanced = (c: Combat) => setCombat(c)
    const handleError = (message: string) => setGmError(message)

    // These events carry DamageDto / ConditionDto, NOT a Combat. Assigning them straight
    // to setCombat wiped `participants`, and CombatPanel then threw on undefined —
    // a white screen in the middle of a fight. Patch the existing state instead.
    const handleDamage = (d: DamageEvent) =>
      setCombat(prev => prev && prev.id === d.combatId
        ? { ...prev, participants: prev.participants.map(p => p.id === d.targetId ? { ...p, hp: d.newHP } : p) }
        : prev)

    const handleHealed = (h: HealEvent) =>
      setCombat(prev => prev && prev.id === h.combatId
        ? { ...prev, participants: prev.participants.map(p => p.id === h.targetId ? { ...p, hp: h.newHP } : p) }
        : prev)

    const handleCondition = (c: ConditionEvent) =>
      setCombat(prev => prev && prev.id === c.combatId
        ? {
            ...prev,
            participants: prev.participants.map(p => {
              if (p.id !== c.participantId) return p
              const current = p.conditions ?? []
              return {
                ...p,
                conditions: c.applied
                  ? (current.includes(c.condition) ? current : [...current, c.condition])
                  : current.filter(x => x !== c.condition),
              }
            }),
          }
        : prev)

    hub.on('NewMessage', handleNewMessage)
    hub.on('Error', handleError)
    hub.on('GMActivity', handleGMActivity)
    hub.on('MessageUpdated', handleMessageUpdated)
    hub.on('MessageRemoved', handleMessageRemoved)
    hub.on('GMError', handleGMError)
    hub.on('CombatStarted', handleCombatStarted)
    hub.on('CombatEnded', handleCombatEnded)
    hub.on('TurnAdvanced', handleTurnAdvanced)
    hub.on('CombatDamageDealt', handleDamage)
    hub.on('CombatHealed', handleHealed)
    hub.on('CombatConditionApplied', handleCondition)
    hub.on('CombatConditionRemoved', handleCondition)

    return () => {
      hub.off('NewMessage', handleNewMessage)
      hub.off('Error', handleError)
      hub.off('GMActivity', handleGMActivity)
      hub.off('MessageUpdated', handleMessageUpdated)
      hub.off('MessageRemoved', handleMessageRemoved)
      hub.off('GMError', handleGMError)
      hub.off('CombatStarted', handleCombatStarted)
      hub.off('CombatEnded', handleCombatEnded)
      hub.off('TurnAdvanced', handleTurnAdvanced)
      hub.off('CombatDamageDealt', handleDamage)
      hub.off('CombatHealed', handleHealed)
      hub.off('CombatConditionApplied', handleCondition)
      hub.off('CombatConditionRemoved', handleCondition)
    }
  }, [hub, appendLive, replaceLive, removeLive])

  useEffect(() => {
    if (isAtBottom.current) bottomRef.current?.scrollIntoView()
  }, [messages])

  const confirmRoll = useCallback(async (toolCallId: string) => {
    try { await api.toolCalls.confirm(toolCallId) }
    catch (e) { setSendError((e as Error).message || 'That roll could not be made.') }
  }, [])

  const declineRoll = useCallback(async (toolCallId: string) => {
    try { await api.toolCalls.decline(toolCallId) }
    catch (e) { setSendError((e as Error).message || 'That roll could not be declined.') }
  }, [])

  /**
   * A roll made for a GM request has a turn held open on it and resolves through
   * /api/gmtools; a roll the player made themselves has nothing waiting, so it resolves over
   * the hub. The prompt message carries which one it is.
   */
  const resolveReroll = useCallback(async (toolCallId: string | null, featureId: string | null) => {
    if (!gameId) return
    try {
      if (toolCallId) {
        await api.toolCalls.reroll(toolCallId, featureId)
      } else if (featureId) {
        await hub.invoke('TakeReroll', gameId, featureId)
      } else {
        await hub.invoke('WaiveReroll', gameId)
      }
    } catch (e) {
      setSendError((e as Error).message || 'That reroll could not be applied.')
    }
  }, [gameId, hub])

  const takeRest = useCallback(async (restType: 'ShortRest' | 'LongRest') => {
    if (!gameId) return
    try {
      await hub.invoke('TakeRest', gameId, restType)
    } catch (e) {
      setSendError((e as Error).message || 'That rest could not be taken.')
    }
  }, [gameId, hub])

  const handleScroll = useCallback(() => {
    const el = chatRef.current
    if (!el) return
    isAtBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80
    if (el.scrollTop < 80 && hasMore && !msgsLoading) loadOlder()
  }, [hasMore, msgsLoading, loadOlder])

  const send = async () => {
    if (!input.trim() || !gameId) return

    // In-character speech needs a character to speak as. The server enforces this too
    // (defense in depth), but failing fast here avoids a round-trip and a raw hub error.
    if (!isOOC && myCharacter === null) {
      setSendError('Create a character before speaking in character. Use OOC for table talk.')
      return
    }

    const content = input.trim()
    setInput('')
    setSendError(null)

    // Hub signatures take no sessionId — the server resolves the active session itself.
    // Passing one shifted every argument by a position and made all four calls fail.
    try {
      if (receiver === 'GM') {
        // The GM is the LLM, not the game's Creator — it has no Player row to whisper.
        // TriggerSuggest asks it directly. NOTE: NarrativeHandler currently broadcasts every
        // response to the whole game group, so this isn't actually private yet — it just
        // stops silently DMing the human who happens to have created the game.
        await hub.invoke('TriggerSuggest', gameId, content)
      } else if (receiver !== 'All') {
        await hub.invoke('SendWhisper', gameId, receiver, content)
      } else if (isOOC) {
        await hub.invoke('SendOOCMessage', gameId, content)
      } else {
        await hub.invoke('SendMessage', gameId, content, false)
        // In-character messages to the table are what the GM narrates against. OOC chat and
        // whispers above stay silent so table banter doesn't spend the owner's LLM budget.
        // Separate try/catch: the message already sent successfully, so a narration failure
        // must not restore it into the input box as if nothing went out.
        try {
          await hub.invoke('TriggerNarrate', gameId, content)
        } catch (narrateErr) {
          setSendError((narrateErr as Error).message || 'The GM could not respond to that.')
        }
      }
    } catch (e) {
      setInput(content)
      setSendError((e as Error).message || 'Could not send message.')
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

      {/* GM Error */}
      {gmError && (
        <Alert severity="error" onClose={() => setGmError(null)} sx={{ mx: 2 }}>
          {gmError}
        </Alert>
      )}

      {/* Send failure — the message text is restored to the input so nothing is lost */}
      {sendError && (
        <Alert severity="error" onClose={() => setSendError(null)} sx={{ mx: 2 }}>
          {sendError}
        </Alert>
      )}

      {/* No character banner — the Creator can also play a character, so this applies to them too */}
      {myCharacter === null && (
        <Alert severity="warning" sx={{ mx: 2 }}
          action={<Button size="small" color="inherit" onClick={() => navigate(`/character/create?gameId=${gameId}`)}>Create Character</Button>}>
          You don't have a character in this game yet.
        </Alert>
      )}

      {/* Resting is a table action, not a private one — it happens here rather than on the
          solo character sheet, and everyone sees it. */}
      {myCharacter && (
        <Box sx={{ px: 2, pb: 1, display: 'flex', gap: 1 }}>
          <Button size="small" variant="outlined" onClick={() => takeRest('ShortRest')}>Short rest</Button>
          <Button size="small" variant="outlined" onClick={() => takeRest('LongRest')}>Long rest</Button>
        </Box>
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
          <MsgBubble
            key={msg.id}
            msg={msg}
            players={players}
            characters={characters}
            prompt={
              <PromptActions
                msg={msg}
                onRoll={confirmRoll}
                onDecline={declineRoll}
                onReroll={resolveReroll}
              />
            }
          />
        ))}
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
          {gmActivity ? (
            <Tooltip title="The GM is working on this turn — nothing's frozen, this can take up to a minute.">
              <Chip
                icon={<CircularProgress size={12} color="inherit" sx={{ ml: '6px !important' }} />}
                label={activityLabel(gmActivity)}
                size="small"
                color="primary"
                sx={{ ml: 1 }}
              />
            </Tooltip>
          ) : game?.gmStatus === 'Running' ? (
            <Chip label="GM Active" size="small" color="primary" sx={{ ml: 1 }} />
          ) : game?.gmStatus === 'Paused' ? (
            <Chip label="GM Paused" size="small" variant="outlined" sx={{ ml: 1 }} />
          ) : null}
          {myCharacter && (
            <Chip
              icon={<CharIcon sx={{ fontSize: '14px !important' }} />}
              label={myCharacter.name}
              size="small"
              variant="outlined"
              onClick={() => navigate(`/character/${myCharacter.id}`)}
              sx={{ ml: 1, cursor: 'pointer' }}
            />
          )}
        </Box>
      </Box>
    </Box>
  )
}
