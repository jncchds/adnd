import { useState } from 'react';
import { useAuth } from '../api/authHook';
import { useGames, useLLMPresets } from '../api/gameHooks';
import {
  Box, Typography, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Alert, Chip, IconButton, Tooltip, AlertTitle, Button
} from '@mui/material';
import { Delete as DeleteIcon, PlayArrow as PlayIcon, Archive as ArchiveIcon,
  Share as ShareIcon, ExitToApp as LeaveIcon } from '@mui/icons-material';
import { Link, useNavigate } from 'react-router-dom';

export default function DashboardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { games, isLoading, error, refetch, createGame, deleteGame, leaveGame, generateInvite, startGame, archiveGame, joinByCode } = useGames();
  const { presets, isLoading: presetsLoading } = useLLMPresets();
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [showJoinDialog, setShowJoinDialog] = useState(false);
  const [joinCode, setJoinCode] = useState('');
  const [gameName, setGameName] = useState('');
  const [systemId, setSystemId] = useState('dnd5e');
  const [llmPresetId, setLLMPresetId] = useState<string | null>(null);
  const [plotSeed, setPlotSeed] = useState('');
  const [gameParameters, setGameParameters] = useState('');
  const [errorState, setErrorState] = useState<string | null>(null);
  const [successState, setSuccessState] = useState<string | null>(null);

  const handleOpenCreate = () => {
    setShowCreateDialog(true);
  };

  const handleCloseCreate = () => {
    setShowCreateDialog(false);
    setGameName('');
    setLLMPresetId(null);
    setPlotSeed('');
    setGameParameters('');
  };

  const handleOpenJoin = () => {
    setShowJoinDialog(true);
  };

  const handleCloseJoin = () => {
    setShowJoinDialog(false);
    setJoinCode('');
  };

  const handleJoin = async () => {
    if (!joinCode.trim()) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      const result: any = await joinByCode(joinCode);
      setShowJoinDialog(false);
      setJoinCode('');
      setSuccessState(`Joined game! Navigating...`);
      setTimeout(() => navigate(`/game/${result.gameId}`), 500);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleCreate = async () => {
    if (!gameName.trim()) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      await createGame(gameName, systemId, undefined, undefined, llmPresetId || undefined, plotSeed || undefined, gameParameters || undefined);
      handleCloseCreate();
      refetch();
      setSuccessState('Game created!');
      setTimeout(() => setSuccessState(null), 2000);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleCopyInvite = async (gameId: string) => {
    try {
      const result = await generateInvite(gameId);
      await navigator.clipboard.writeText(result.inviteUrl);
      setSuccessState('Invite URL copied!');
      setTimeout(() => setSuccessState(null), 2000);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleStartGame = async (gameId: string) => {
    setErrorState(null);
    try {
      await startGame(gameId);
      refetch();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleArchiveGame = async (gameId: string) => {
    setErrorState(null);
    try {
      await archiveGame(gameId);
      refetch();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const statusColor = (status: string) => {
    switch (status) {
      case 'Active': return 'success';
      case 'Draft': return 'warning';
      case 'Archived': return 'default';
      case 'Finished': return 'info';
      default: return 'default';
    }
  };

  if (isLoading) {
    return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading games...</Typography></Box>;
  }

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Box>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            Welcome, {user?.displayName || 'Player'}!
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Manage your games and characters
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button variant="outlined" size="small" onClick={() => navigate('/llm-presets')}>LLM Presets</Button>
          <Button variant="outlined" onClick={handleOpenJoin}>Join by Code</Button>
          <Button variant="contained" onClick={handleOpenCreate}>+ New Game</Button>
        </Box>
      </Box>

      {/* Messages */}
      {successState && (
        <Alert severity="success" onClose={() => setSuccessState(null)} sx={{ mb: 2, alignItems: 'center' }}>
          {successState}
        </Alert>
      )}
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {error}
        </Alert>
      )}

      {/* Create Game Dialog */}
      <Dialog open={showCreateDialog} onClose={handleCloseCreate} maxWidth="md" fullWidth>
        <DialogTitle>Create New Game</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            fullWidth
            label="Game Name"
            value={gameName}
            onChange={e => setGameName(e.target.value)}
            autoFocus
          />
          <TextField
            fullWidth
            select
            label="System"
            value={systemId}
            onChange={e => setSystemId(e.target.value)}
            SelectProps={{ native: true }}
          >
            <option value="dnd5e">D&D 5th Edition</option>
            <option value="pf2e">Pathfinder 2nd Edition</option>
            <option value="coc7e">Call of Cthulhu 7th Edition</option>
          </TextField>
          <TextField
            fullWidth
            select
            label="LLM Preset (for AI-GM)"
            value={llmPresetId || ''}
            onChange={e => setLLMPresetId(e.target.value || null)}
            SelectProps={{ native: true }}
            disabled={presetsLoading}
          >
            <option value="">None</option>
            {presets.map(p => (
              <option key={p.id} value={p.id}>{p.name} ({p.providerType} / {p.baseModel})</option>
            ))}
          </TextField>
          <TextField
            fullWidth
            multiline
            rows={3}
            label="Plot Seed (initial story premise)"
            value={plotSeed}
            onChange={e => setPlotSeed(e.target.value)}
            placeholder="Describe the initial story, setting, and tone..."
          />
          <TextField
            fullWidth
            multiline
            rows={2}
            label="Game Parameters (tone, difficulty, pacing)"
            value={gameParameters}
            onChange={e => setGameParameters(e.target.value)}
            placeholder="e.g., Dark tone, medium difficulty, fast-paced..."
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={handleCloseCreate}>Cancel</Button>
          <Button onClick={handleCreate} variant="contained" disabled={!gameName.trim()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>

      {/* Join by Code Dialog */}
      <Dialog open={showJoinDialog} onClose={handleCloseJoin} maxWidth="sm" fullWidth>
        <DialogTitle>Join a Game</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Enter the invite code shared by the game creator to join their game.
          </Typography>
          <TextField
            fullWidth
            label="Invite Code"
            value={joinCode}
            onChange={e => setJoinCode(e.target.value)}
            placeholder="e.g., abc12345 or /join/abc12345"
            autoFocus
            onKeyDown={e => e.key === 'Enter' && handleJoin()}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={handleCloseJoin}>Cancel</Button>
          <Button onClick={handleJoin} variant="contained" disabled={!joinCode.trim()}>
            Join
          </Button>
        </DialogActions>
      </Dialog>

      {/* Games Table */}
      <Paper elevation={2}>
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow sx={{ bgcolor: 'background.paper' }}>
                <TableCell><strong>Game</strong></TableCell>
                <TableCell><strong>System</strong></TableCell>
                <TableCell><strong>Status</strong></TableCell>
                <TableCell><strong>Created</strong></TableCell>
                <TableCell align="right"><strong>Actions</strong></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {games.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 4 }}>
                    <Typography color="text.secondary" sx={{ mb: 2 }}>No games yet</Typography>
                    <Typography variant="body2" color="text.secondary">
                      Use the sidebar to create a new game or join one by code.
                    </Typography>
                  </TableCell>
                </TableRow>
              ) : (
                games.map(game => (
                  <TableRow key={game.id} hover>
                    <TableCell>
                      <Typography variant="body1">{game.name}</Typography>
                      <Typography variant="body2" color="text.secondary">
                        by {game.creatorName}
                      </Typography>
                      {game.llmPresetName && (
                        <Typography variant="caption" color="primary">
                          LLM: {game.llmPresetName}
                        </Typography>
                      )}
                      {game.status === 'Active' && game.inviteCode && (
                        <Typography variant="caption" color="text.secondary">
                          Code: <strong>{game.inviteCode}</strong>
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Chip label={game.systemId} size="small" variant="outlined" />
                    </TableCell>
                    <TableCell>
                      <Chip label={game.status} size="small" color={statusColor(game.status) as any} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">
                        {new Date(game.createdAt).toLocaleDateString()}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title="Play">
                        <IconButton component={Link} to={`/game/${game.id}`} size="small">
                          <PlayIcon />
                        </IconButton>
                      </Tooltip>
                      {game.status === 'Active' && game.inviteCode && (
                        <Tooltip title={game.inviteCode}>
                          <span>
                            <Chip label={game.inviteCode} size="small" variant="outlined" sx={{ mr: 0.5 }} />
                          </span>
                        </Tooltip>
                      )}
                      <Tooltip title="Copy Invite Code">
                        <IconButton onClick={() => handleCopyInvite(game.id)} size="small">
                          <ShareIcon />
                        </IconButton>
                      </Tooltip>
                      {game.status === 'Draft' && (
                        <>
                          <Tooltip title="Start Game">
                            <IconButton size="small" color="success" onClick={() => handleStartGame(game.id)}>
                              <PlayIcon />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Archive">
                            <IconButton size="small" color="default" onClick={() => handleArchiveGame(game.id)}>
                              <ArchiveIcon />
                            </IconButton>
                          </Tooltip>
                        </>
                      )}
                      {game.status === 'Active' && (
                        <Tooltip title="Archive">
                          <IconButton size="small" color="default" onClick={() => handleArchiveGame(game.id)}>
                            <ArchiveIcon />
                          </IconButton>
                        </Tooltip>
                      )}
                      {game.status === 'Active' && (
                        <Tooltip title="Leave">
                          <IconButton onClick={() => leaveGame(game.id)} size="small" color="error">
                            <LeaveIcon />
                          </IconButton>
                        </Tooltip>
                      )}
                      <Tooltip title="Delete">
                        <IconButton onClick={() => deleteGame(game.id)} size="small" color="error">
                          <DeleteIcon />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>
    </Box>
  );
}
