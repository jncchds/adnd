import { Box, Typography, Button, Paper } from '@mui/material'
import { SportsEsports as DiceIcon, Groups as GroupsIcon,
  AutoAwesome as LLMIcon, Speed as SpeedIcon,
  Palette as PaletteIcon, Shield as ShieldIcon } from '@mui/icons-material'
import { Link as RouterLink } from 'react-router-dom'

export default function HomePage() {
  return (
    <Box sx={{ textAlign: 'center', py: 4 }}>
      {/* Hero */}
      <Box sx={{ mb: 6 }}>
        <Typography variant="h2" component="h1" gutterBottom sx={{ fontWeight: 700 }}>
          Welcome to ADnD
        </Typography>
        <Typography variant="h5" color="text.secondary" sx={{ mb: 2 }}>
          Advanced Dungeon Network
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{ mb: 4, maxWidth: 600, mx: 'auto' }}>
          Your multi-system TTRPG framework with LLM-powered Game Master assistance,
          real-time chat, and custom system support.
        </Typography>
        <Button variant="contained" size="large" component={RouterLink} to="/dashboard" sx={{ mr: 2 }}>
          Go to Dashboard
        </Button>
        <Button variant="outlined" size="large" component={RouterLink} to="/register">
          Create Account
        </Button>
      </Box>

      {/* Features */}
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 3, mb: 4 }}>
        {[
          { icon: <DiceIcon fontSize="large" />, title: 'Multi-System Support', desc: 'D&D 5e, Pathfinder 2e, Call of Cthulhu 7e, and custom systems' },
          { icon: <SpeedIcon fontSize="large" />, title: 'Real-Time Chat', desc: 'SignalR-powered game rooms with dice rolling and action tracking' },
          { icon: <LLMIcon fontSize="large" />, title: 'LLM Game Master', desc: 'Pluggable AI assistant for plot suggestions and consistency checks' },
          { icon: <GroupsIcon fontSize="large" />, title: 'NPC & Plot Management', desc: 'Track NPCs, plot threads, and game events with RAG-powered context' },
          { icon: <PaletteIcon fontSize="large" />, title: 'Custom Systems', desc: 'Define your own RPG systems with custom attributes, skills, and rules' },
          { icon: <ShieldIcon fontSize="large" />, title: 'Secure Auth', desc: 'JWT-based authentication with refresh tokens and role management' },
        ].map((feature, i) => (
          <Paper key={i} elevation={2} sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <Box sx={{ textAlign: 'center', p: 3, flexGrow: 1 }}>
              <Box sx={{ color: 'primary.main', mb: 1 }}>{feature.icon}</Box>
              <Typography variant="h6" gutterBottom>{feature.title}</Typography>
              <Typography variant="body2" color="text.secondary">{feature.desc}</Typography>
            </Box>
          </Paper>
        ))}
      </Box>

      {/* Quick Start */}
      <Paper elevation={2} sx={{ p: 4, maxWidth: 700, mx: 'auto' }}>
        <Typography variant="h5" gutterBottom>Quick Start</Typography>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 2, mt: 1 }}>
          <Box>
            <Typography variant="subtitle1" color="primary">1. Create Account</Typography>
            <Typography variant="body2" color="text.secondary">Sign up to get started</Typography>
            <Button size="small" component={RouterLink} to="/register" sx={{ mt: 1 }}>Register</Button>
          </Box>
          <Box>
            <Typography variant="subtitle1" color="primary">2. Create a Game</Typography>
            <Typography variant="body2" color="text.secondary">Choose your RPG system</Typography>
            <Button size="small" component={RouterLink} to="/dashboard" sx={{ mt: 1 }}>Dashboard</Button>
          </Box>
          <Box>
            <Typography variant="subtitle1" color="primary">3. Invite Players</Typography>
            <Typography variant="body2" color="text.secondary">Share your invite code</Typography>
            <Button size="small" component={RouterLink} to="/dashboard" sx={{ mt: 1 }}>Start Playing</Button>
          </Box>
        </Box>
      </Paper>

      {/* Footer */}
      <Typography variant="body2" color="text.secondary" sx={{ mt: 6 }}>
        ADnD v0.1 · Built with ASP.NET Core 10, React 19, PostgreSQL + PGVector
      </Typography>
    </Box>
  )
}
