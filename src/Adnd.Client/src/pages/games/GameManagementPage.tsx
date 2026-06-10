import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Container, Paper, Typography, Box, Button, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Dialog, DialogTitle, DialogContent,
  DialogActions, TextField, MenuItem, Chip, Alert, Stack, Divider,
  List, ListItem, ListItemText, ListItemAvatar, Avatar, IconButton,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import KeyIcon from '@mui/icons-material/Key';
import LogoutIcon from '@mui/icons-material/Logout';
import RefreshIcon from '@mui/icons-material/Refresh';
import CopyIcon from '@mui/icons-material/ContentCopy';
import BlockIcon from '@mui/icons-material/Block';
import client from '@/api/client';

interface Game {
  id: string;
  title: string;
  systemId: string;
  systemName: string;
  systemSlug: string;
  creatorId: string;
  creatorDisplayName: string;
  status: string;
  plotSeed?: string;
  joinCode: string;
  playerCount: number;
  createdAt: string;
  updatedAt: string;
}
interface GamePlayer {
  id: string;
  userId: string;
  role: string;
  characterName: string;
  spectating: boolean;
  isBanned: boolean;
}
interface GameDetail extends Game {
  players: GamePlayer[];
}
interface GameSystem {
  id: string;
  name: string;
  slug: string;
  description: string;
  type: string;
}

export default function GameManagementPage() {
  const navigate = useNavigate();
  const [games, setGames] = useState<Game[]>([]);
  const [systems, setSystems] = useState<GameSystem[]>([]);
  const [detail, setDetail] = useState<GameDetail | null>(null);
  const [openCreate, setOpenCreate] = useState(false);
  const [openJoinCode, setOpenJoinCode] = useState(false);
  const [joinCodeInput, setJoinCodeInput] = useState('');
  const [copiedCode, setCopiedCode] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const fetchGames = async () => {
    const res = await client.get('/games');
    setGames(res.data);
  };

  const fetchSystems = async () => {
    const res = await client.get('/systems');
    setSystems(res.data);
  };

  const fetchDetail = async (id: string) => {
    const res = await client.get(`/games/${id}`);
    setDetail(res.data);
  };

  useEffect(() => { fetchGames(); fetchSystems(); }, []);

  const handleOpenCreate = () => {
    setOpenCreate(true);
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const form = new FormData(e.target as HTMLFormElement);
    const title = form.get('title') as string;
    const systemId = form.get('systemId') as string;
    const plotSeed = form.get('plotSeed') as string;

    setError('');
    try {
      await client.post('/games', { title, systemId, plotSeed: plotSeed || undefined });
      setSuccess('Game created');
      setTimeout(() => setSuccess(''), 3000);
      setOpenCreate(false);
      fetchGames();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to create game');
    }
  };

  const handleJoin = async (id: string) => {
    await client.post(`/games/${id}/join`);
    setSuccess('Joined game');
    setTimeout(() => setSuccess(''), 3000);
    fetchGames();
  };

  const handleLeave = async (id: string) => {
    await client.post(`/games/${id}/leave`);
    setSuccess('Left game');
    setTimeout(() => setSuccess(''), 3000);
    setDetail(null);
    fetchGames();
  };

  const handleJoinByCode = async () => {
    setError('');
    try {
      await client.post('/games/join-by-code', { joinCode: joinCodeInput.toUpperCase() });
      setSuccess('Joined game');
      setTimeout(() => setSuccess(''), 3000);
      setOpenJoinCode(false);
      setJoinCodeInput('');
      fetchGames();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to join');
    }
  };

  const handleBanPlayer = async (gameId: string, playerId: string) => {
    try {
      await client.post(`/games/${gameId}/ban/${playerId}`);
      setDetail(prev => prev ? {
        ...prev,
        players: prev.players.map(p => p.id === playerId ? { ...p, isBanned: true } : p)
      } : null);
      setSuccess('Player banned');
      setTimeout(() => setSuccess(''), 3000);
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to ban');
    }
  };

  const handleUnbanPlayer = async (gameId: string, playerId: string) => {
    try {
      await client.post(`/games/${gameId}/unban/${playerId}`);
      setDetail(prev => prev ? {
        ...prev,
        players: prev.players.map(p => p.id === playerId ? { ...p, isBanned: false } : p)
      } : null);
      setSuccess('Player unbanned');
      setTimeout(() => setSuccess(''), 3000);
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to unban');
    }
  };

  const copyCode = async (code: string) => {
    await navigator.clipboard.writeText(code);
    setCopiedCode(code);
    setTimeout(() => setCopiedCode(''), 2000);
  };

  const handleRegenerateCode = async (gameId: string) => {
    try {
      const res = await client.post(`/games/${gameId}/regenerate-code`);
      setDetail(prev => prev ? { ...prev, joinCode: res.data.joinCode } : null);
      setSuccess('Join code regenerated');
      setTimeout(() => setSuccess(''), 3000);
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to regenerate');
    }
  };

  const handleViewDetail = (game: Game) => {
    fetchDetail(game.id);
  };

  const statusColor = (status: string) => {
    switch (status) {
      case 'Draft': return 'default';
      case 'Active': return 'success';
      case 'Archived': return 'error';
      default: return 'default';
    }
  };

  const roleColor = (role: string) => {
    switch (role) {
      case 'Creator': return 'primary';
      case 'Gm': return 'secondary';
      case 'Player': return 'default';
      default: return 'default';
    }
  };

  return (
    <Container maxWidth="md">
      <Box sx={{ mt: 4 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
          <Typography variant="h4">My Games</Typography>
          <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenCreate}>
            New Game
          </Button>
        </Box>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {success && <Alert severity="success" sx={{ mb: 2 }}>{success}</Alert>}

        <Button variant="outlined" startIcon={<KeyIcon />} sx={{ mb: 2 }} onClick={() => setOpenJoinCode(true)}>
          Join by Code
        </Button>

        {!detail ? (
          <TableContainer component={Paper}>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Title</TableCell>
                  <TableCell>System</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Players</TableCell>
                  <TableCell>Created</TableCell>
                  <TableCell>Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {games.map((game) => (
                  <TableRow key={game.id}>
                    <TableCell>{game.title}</TableCell>
                    <TableCell>{game.systemName}</TableCell>
                    <TableCell>
                      <Chip label={game.status} color={statusColor(game.status)} size="small" />
                    </TableCell>
                    <TableCell>{game.playerCount}</TableCell>
                    <TableCell>{new Date(game.createdAt).toLocaleDateString()}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={1}>
                        <Button size="small" onClick={() => handleViewDetail(game)}>View</Button>
                        {game.status !== 'Archived' && (
                          <Button size="small" variant="outlined" onClick={() => handleJoin(game.id)}>
                            {game.playerCount > 0 ? 'Join' : 'Play'}
                          </Button>
                        )}
                      </Stack>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        ) : (
          <Box>
            <Button sx={{ mb: 2 }} onClick={() => setDetail(null)}>Back to Games</Button>
            <Paper sx={{ p: 3 }}>
              <Typography variant="h5" gutterBottom>{detail.title}</Typography>
              <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
                <Chip label={detail.systemName} size="small" />
                <Chip label={detail.status} color={statusColor(detail.status)} size="small" />
                <Typography variant="body2" color="text.secondary">
                  Created by {detail.creatorDisplayName}
                </Typography>
              </Stack>
              {detail.plotSeed && (
                <Box sx={{ mb: 2 }}>
                  <Typography variant="subtitle2" gutterBottom>Plot Seed</Typography>
                  <Typography variant="body2">{detail.plotSeed}</Typography>
                </Box>
              )}
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
                <Typography variant="subtitle2">Join Code:</Typography>
                <Typography variant="h6" sx={{ fontFamily: 'monospace', letterSpacing: 2 }}>{detail.joinCode}</Typography>
                <IconButton size="small" onClick={() => copyCode(detail.joinCode)} title="Copy code">
                  <CopyIcon />
                </IconButton>
                {detail.creatorId && (
                  <IconButton size="small" onClick={() => handleRegenerateCode(detail.id)} title="Regenerate code">
                    <RefreshIcon />
                  </IconButton>
                )}
              </Box>
              <Divider sx={{ my: 2 }} />
              <Typography variant="subtitle1" gutterBottom>Players ({detail.players.length})</Typography>
              <List>
                {detail.players.map((player) => (
                  <ListItem key={player.id} sx={{ opacity: player.isBanned ? 0.5 : 1 }}>
                    <ListItemAvatar>
                      <Avatar>{player.characterName?.[0] || player.userId[0].toUpperCase()}</Avatar>
                    </ListItemAvatar>
                    <ListItemText
                      primary={player.characterName || 'Unnamed'}
                      secondary={player.role + (player.isBanned ? ' (Banned)' : '')}
                    />
                    <Chip label={player.role} color={roleColor(player.role)} size="small" />
                    {detail.creatorId && player.role !== 'Creator' && (
                      player.isBanned ? (
                        <Button size="small" onClick={() => handleUnbanPlayer(detail.id, player.id)}>Unban</Button>
                      ) : (
                        <Button size="small" color="error" onClick={() => handleBanPlayer(detail.id, player.id)}>Ban</Button>
                      )
                    )}
                  </ListItem>
                ))}
              </List>
              <Divider sx={{ my: 2 }} />
              <Button variant="outlined" color="error" onClick={() => handleLeave(detail.id)} startIcon={<LogoutIcon />}>
                Leave
              </Button>
            </Paper>
          </Box>
        )}

        <Dialog open={openCreate} onClose={() => setOpenCreate(false)} maxWidth="sm" fullWidth>
          <DialogTitle>Create New Game</DialogTitle>
          <Box component="form" onSubmit={handleCreate} sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField
              fullWidth
              name="title"
              label="Game Title"
              required
            />
            <TextField
              fullWidth
              name="systemId"
              label="Game System"
              select
              required
              helperText="Select the TTRPG system for this game"
            >
              {systems
                .filter(s => s.type === 'predefined')
                .map((system) => (
                  <MenuItem key={system.id} value={system.id}>
                    {system.name}
                  </MenuItem>
                ))}
            </TextField>
            <TextField
              fullWidth
              name="plotSeed"
              label="Plot Seed (optional)"
              multiline
              rows={3}
              helperText="Brief description of the game premise"
            />
            <DialogActions>
              <Button onClick={() => setOpenCreate(false)}>Cancel</Button>
              <Button type="submit" variant="contained">Create</Button>
            </DialogActions>
          </Box>
        </Dialog>

        <Dialog open={openJoinCode} onClose={() => setOpenJoinCode(false)} maxWidth="sm">
          <DialogTitle>Join by Code</DialogTitle>
          <DialogContent sx={{ mt: 2, display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField
              fullWidth
              label="8-character join code"
              value={joinCodeInput}
              onChange={(e) => setJoinCodeInput(e.target.value.toUpperCase())}
              inputProps={{ maxLength: 8, style: { fontFamily: 'monospace', letterSpacing: 4, textAlign: 'center', fontSize: '1.5rem' } }}
              helperText="Enter the code shared by the game creator"
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setOpenJoinCode(false)}>Cancel</Button>
            <Button onClick={handleJoinByCode} variant="contained" disabled={joinCodeInput.length !== 8}>Join</Button>
          </DialogActions>
        </Dialog>
      </Box>
    </Container>
  );
}
