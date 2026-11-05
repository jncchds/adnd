import { useCallback, useEffect, useState } from 'react'
import {
  Alert, Box, Button, CircularProgress, Divider, Link, List, ListItem, ListItemText, Paper, Typography,
} from '@mui/material'
import { api } from '../api/client'
import type { GameTelegram, TelegramAccount, TelegramBotStatus, TelegramCode } from '../types'

/** Why the bot can't be used right now, or null when it can. */
function botUnavailable(bot: TelegramBotStatus): string | null {
  if (!bot.enabled) return 'The Telegram bot is off on this server (TELEGRAM_BOT_TOKEN is not set).'
  if (!bot.ready) return bot.problem ?? 'The Telegram bot is starting…'
  return null
}

function CodeBox({ code, linkLabel, hint }: { code: TelegramCode; linkLabel: string; hint: string }) {
  const expires = new Date(code.expiresAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  return (
    <Alert severity="info" sx={{ mt: 1.5 }}>
      {code.link && (
        <Box sx={{ mb: 0.5 }}>
          <Link href={code.link} target="_blank" rel="noreferrer">{linkLabel}</Link>
        </Box>
      )}
      <Typography variant="body2">
        {hint} <code>{code.command}</code>
      </Typography>
      <Typography variant="caption" color="text.secondary">Works once, until {expires}.</Typography>
    </Alert>
  )
}

/** User Settings: link this account to Telegram, so the bot knows who is speaking and whose prompts are whose. */
export function TelegramAccountPanel() {
  const [account, setAccount] = useState<TelegramAccount | null>(null)
  const [code, setCode] = useState<TelegramCode | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      setAccount(await api.telegram.account())
      setError(null)
    } catch (e) { setError((e as Error).message) }
  }, [])

  useEffect(() => { load() }, [load])

  const makeCode = async () => {
    setBusy(true)
    setError(null)
    try { setCode(await api.telegram.createLinkCode()) }
    catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const unlink = async () => {
    setBusy(true)
    setError(null)
    try {
      await api.telegram.unlink()
      setCode(null)
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const unavailable = account ? botUnavailable(account.bot) : null

  return (
    <Paper sx={{ p: 2, mt: 2 }}>
      <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 1 }}>Telegram</Typography>
      <Divider sx={{ mb: 2 }} />
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}
      {!account ? (
        !error && <CircularProgress size={18} />
      ) : unavailable ? (
        <Typography variant="body2" color="text.secondary">{unavailable}</Typography>
      ) : account.linkedUsername ? (
        <Box>
          <Typography variant="body2" sx={{ mb: 1 }}>
            Linked to <strong>{account.linkedUsername}</strong>
            {account.bot.botUsername && <> — talk to <Link href={`https://t.me/${account.bot.botUsername}`} target="_blank" rel="noreferrer">@{account.bot.botUsername}</Link></>}.
            Roll prompts and whispers from your games arrive there, and in a group that plays your game your messages are your character speaking.
          </Typography>
          <Button variant="outlined" color="inherit" onClick={unlink} disabled={busy}>Unlink</Button>
        </Box>
      ) : (
        <Box>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Play from Telegram: link your account, then pick a game in the bot's private chat or play in a group your GM connected.
          </Typography>
          <Button variant="contained" onClick={makeCode} disabled={busy}>
            {busy ? <CircularProgress size={18} /> : 'Link Telegram'}
          </Button>
          {code && (
            <CodeBox code={code} linkLabel="Open the bot and press Start"
              hint="Or send it this in a private chat:" />
          )}
          {code && (
            <Button size="small" sx={{ mt: 1 }} onClick={load}>I've linked it</Button>
          )}
        </Box>
      )}
    </Paper>
  )
}

/** Game Settings (creator): connect Telegram groups or topics to this game, and disconnect them. */
export function GameTelegramPanel({ gameId }: { gameId: string }) {
  const [state, setState] = useState<GameTelegram | null>(null)
  const [code, setCode] = useState<TelegramCode | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      setState(await api.telegram.forGame(gameId))
      setError(null)
    } catch (e) { setError((e as Error).message) }
  }, [gameId])

  useEffect(() => { load() }, [load])

  const makeCode = async () => {
    setBusy(true)
    setError(null)
    try { setCode(await api.telegram.createBindCode(gameId)) }
    catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const revoke = async (bindingId: string) => {
    setBusy(true)
    setError(null)
    try {
      await api.telegram.revokeBinding(gameId, bindingId)
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const unavailable = state ? botUnavailable(state.bot) : null

  return (
    <Paper sx={{ p: 2, mb: 2 }}>
      <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 1 }}>Telegram</Typography>
      <Divider sx={{ mb: 2 }} />
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}
      {!state ? (
        !error && <CircularProgress size={18} />
      ) : unavailable ? (
        <Typography variant="body2" color="text.secondary">{unavailable}</Typography>
      ) : (
        <Box>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            A connected group (or forum topic) is the table: it sees what everyone sees, linked players speak in character
            by writing there, and the fight shows as a pinned card. Turn the bot's privacy mode off in @BotFather (or make it
            an admin) so it can read the group.
          </Typography>
          {state.bindings.length > 0 && (
            <List dense disablePadding sx={{ mb: 1 }}>
              {state.bindings.map(b => (
                <ListItem key={b.id} disableGutters
                  secondaryAction={<Button size="small" color="inherit" disabled={busy} onClick={() => revoke(b.id)}>Disconnect</Button>}>
                  <ListItemText primary={b.chatTitle}
                    secondary={`Connected ${new Date(b.createdAt).toLocaleDateString()}`} />
                </ListItem>
              ))}
            </List>
          )}
          <Button variant="outlined" onClick={makeCode} disabled={busy}>
            {busy ? <CircularProgress size={18} /> : 'Connect a group'}
          </Button>
          {code && (
            <CodeBox code={code} linkLabel="Add the bot to a group"
              hint="Or, in a group or forum topic the bot is already in, send:" />
          )}
          {code && <Button size="small" sx={{ mt: 1 }} onClick={load}>Refresh</Button>}
        </Box>
      )}
    </Paper>
  )
}
