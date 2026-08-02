import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Box, Typography, Button, Card, CardContent, Chip, useTheme } from '@mui/material'
import { alpha } from '@mui/material/styles'
import {
  Casino as DiceIcon,
  SmartToy as GMIcon,
  Groups as MultiplayerIcon,
  AccountTree as PlotIcon,
  TheaterComedy as NPCIcon,
  Person as CharacterIcon,
  Shield as CombatIcon,
  Tune as PresetsIcon,
  AdminPanelSettings as AdminIcon,
} from '@mui/icons-material'

interface Capability {
  icon: ReactNode
  title: string
  body: string
}

const CAPABILITIES: Capability[] = [
  {
    icon: <GMIcon />,
    title: 'An AI Game Master',
    body: 'The GM narrates, calls for rolls, resolves them and moves the world on its own — '
      + 'through a real tool loop, not a chat window pretending to be one.',
  },
  {
    icon: <MultiplayerIcon />,
    title: 'A real table, in real time',
    body: 'Multiplayer sessions over a live connection: in-character lines, out-of-character '
      + 'talk, and private whispers that stay private — including from the narrator.',
  },
  {
    icon: <DiceIcon />,
    title: 'Dice the GM actually waits for',
    body: 'Skill checks, saving throws, attacks and secret rolls. When a feature gives you a '
      + 'reroll, you are offered it — and the GM holds its turn until you have decided.',
  },
  {
    icon: <PlotIcon />,
    title: 'Plot that remembers',
    body: 'Sessions are embedded and searched, so threads you opened ten scenes ago come back '
      + 'on their own. A background weaver adapts and seeds new ones as the campaign moves.',
  },
  {
    icon: <NPCIcon />,
    title: 'NPCs with a life of their own',
    body: 'People the GM introduces become real records with a status and a history, ranked by '
      + 'who is actually in the room — so the innkeeper is still the same innkeeper.',
  },
  {
    icon: <CharacterIcon />,
    title: 'Characters in a few minutes',
    body: 'A guided wizard that starts from who your character is. One button fills in the '
      + 'rest from the campaign itself — grounded in the plot and the rest of the party.',
  },
  {
    icon: <CombatIcon />,
    title: 'Combat, tracked',
    body: 'Initiative order, participants, conditions and loot handled by the system while the '
      + 'narration stays prose.',
  },
  {
    icon: <PresetsIcon />,
    title: 'Bring your own model',
    body: 'Ollama, OpenAI, Google or any OpenAI-compatible endpoint. Keys are encrypted at '
      + 'rest, and each campaign picks its own preset.',
  },
  {
    icon: <AdminIcon />,
    title: 'A console for the GM',
    body: 'Plot board, NPC roster, consistency checks and the full log of every LLM call the '
      + 'campaign has made — nothing about the machinery is hidden from you.',
  },
]

export default function LandingPage() {
  const navigate = useNavigate()
  const theme = useTheme()

  return (
    <Box sx={{ px: { xs: 2, md: 6 }, py: { xs: 4, md: 8 }, maxWidth: 1180, mx: 'auto', width: '100%' }}>
      {/* Hero */}
      <Box sx={{ textAlign: 'center', mb: { xs: 6, md: 9 } }}>
        <DiceIcon sx={{ fontSize: 64, color: 'primary.main' }} />
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 1.5, mt: 1 }}>
          <Typography variant="h2" fontWeight={800} color="primary" sx={{ letterSpacing: 2 }}>
            ADnD
          </Typography>
          <Chip label="ALPHA" size="small" color="error" sx={{ fontWeight: 700, height: 20 }} />
        </Box>
        <Typography variant="h5" sx={{ mt: 2, fontWeight: 400 }}>
          Tabletop roleplaying with an AI Game Master
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{ mt: 2, maxWidth: 680, mx: 'auto' }}>
          Gather a party, hand the screen to a narrator that remembers your campaign, and play.
          Bring your own language model — or run one locally and keep the whole table on your
          own machine.
        </Typography>
        <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center', mt: 4, flexWrap: 'wrap' }}>
          <Button variant="contained" size="large" onClick={() => navigate('/register')} sx={{ px: 4 }}>
            Create an account
          </Button>
          <Button variant="outlined" size="large" onClick={() => navigate('/login')} sx={{ px: 4 }}>
            Sign in
          </Button>
        </Box>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 3 }}>
          v{__APP_VERSION__} — early and moving fast. See what changed on the{' '}
          <Box
            component="a"
            href="/release-notes"
            onClick={e => { e.preventDefault(); navigate('/release-notes') }}
            sx={{ color: 'primary.main', textDecoration: 'none', '&:hover': { textDecoration: 'underline' } }}
          >
            release notes
          </Box>
          .
        </Typography>
      </Box>

      {/* Capability cards */}
      <Box sx={{
        display: 'grid',
        gap: 2.5,
        gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' },
      }}>
        {CAPABILITIES.map(cap => (
          <Card
            key={cap.title}
            variant="outlined"
            sx={{
              height: '100%',
              transition: 'border-color 0.15s, transform 0.15s',
              '&:hover': {
                borderColor: alpha(theme.palette.primary.main, 0.5),
                transform: 'translateY(-2px)',
              },
            }}
          >
            <CardContent>
              <Box sx={{
                width: 40, height: 40, borderRadius: 1.5, mb: 1.5,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                bgcolor: alpha(theme.palette.primary.main, 0.12),
                color: 'primary.main',
              }}>
                {cap.icon}
              </Box>
              <Typography variant="subtitle1" fontWeight={700} gutterBottom>{cap.title}</Typography>
              <Typography variant="body2" color="text.secondary">{cap.body}</Typography>
            </CardContent>
          </Card>
        ))}
      </Box>

      {/* Closing call to action */}
      <Box sx={{
        mt: { xs: 6, md: 9 }, p: { xs: 3, md: 5 }, textAlign: 'center',
        borderRadius: 2,
        border: `1px solid ${alpha(theme.palette.primary.main, 0.25)}`,
        bgcolor: alpha(theme.palette.primary.main, 0.06),
      }}>
        <Typography variant="h5" fontWeight={700}>Roll for initiative</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1, mb: 3 }}>
          Create an account, spin up a campaign, and invite your party with a join code.
        </Typography>
        <Button variant="contained" size="large" onClick={() => navigate('/register')} sx={{ px: 4 }}>
          Get started
        </Button>
      </Box>
    </Box>
  )
}
