import { useState } from 'react';
import { useAuth } from '../api/authHook';
import { useGames } from '../api/gameHooks';
import {
  Container, Box, Typography, Button, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Alert, Chip, IconButton, Tooltip, AlertTitle
} from '@mui/material';
import { Delete as DeleteIcon, Add as AddIcon, PlayArrow as PlayIcon, Archive as ArchiveIcon,
  Share as ShareIcon, ExitToApp as LeaveIcon } from '@mui/icons-material';
import { Link } from 'react-router-dom';

export default function DashboardPage() {
  const { user, logout } = useAuth();
  const { games, isLoading, error, refetch, createGame, deleteGame, joinGame, leaveGame, generateInvite, startGame, archiveGame } = useGames();
  const [openDialog, setOpenDialog] = useState(false);
  const [gameName, setGameName] = useState('');
  const [systemId, setSystemId] = useState('dnd5e');
  const [joinCode, setJoinCode] = useState('');
  const [joinResult, setJoinResult] = useState<string | null>(null);
  const [errorState, setErrorState] = useState<string | null>(null);
  const [openJoinDialog, setOpenJoinDialog] = useState(false);

  const handleCreate = async () => {
    if (!gameName.trim()) return;
    setErrorState(null);
    try {
      await createGame(gameName, systemId);
      setGameName('');
      setOpenDialog(false);
      refetch();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleJoin = async () => {
    setErrorState(null);
    setJoinResult(null);
    try {
      await joinGame(joinCode);
      setJoinCode('');
      setJoinResult('Successfully joined the game!');
      refetch();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleCopyInvite = async (gameId: string) => {
    try {
      const result = await generateInvite(gameId);
      await navigator.clipboard.writeText(result.inviteUrl);
      setJoinResult(`Invite URL copied: ${result.inviteUrl}`);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleStartGame = async (gameId: string) => {
    setErrorState(null);
    try {
      await startGame(gameId);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleArchiveGame = async (gameId: string) => {
    setErrorState(null);
    try {
      await archiveGame(gameId);
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
    <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 4 }}>
        <Box>
          <Typography variant="h4">Welcome, {user?.displayName || 'Player'}!</Typography>
          <Typography variant="body2" color="text.secondary">
            Manage your games and characters
          </Typography>
        </Box>
        <Box>
          <Button variant="outlined" onClick={() => setOpenDialog(true)} sx={{ mr: 1 }}>
            <AddIcon sx={{ mr: 1 }} /> New Game
          </Button>
          <Button variant="outlined" onClick={() => setOpenJoinDialog(true)} sx={{ mr: 1 }}>
            <AddIcon sx={{ mr: 1 }} /> Join by Code
          </Button>
          <Button variant="outlined" onClick={logout}>Logout</Button>
        </Box>
      </Box>

      {/* Error */}
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}

      {/* Create Game Dialog */}
      <Dialog open={openDialog} onClose={() => setOpenDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Create New Game</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <TextField
            fullWidth
            label="Game Name"
            value={gameName}
            onChange={e => setGameName(e.target.value)}
            sx={{ mb: 2 }}
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
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenDialog(false)}>Cancel</Button>
          <Button onClick={handleCreate} variant="contained" disabled={!gameName.trim()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>

      {/* Join by Code Dialog */}
      <Dialog open={openJoinDialog} onClose={() => { setOpenJoinDialog(false); setJoinResult(null); setJoinCode(''); }} maxWidth="sm" fullWidth>
        <DialogTitle>Join a Game</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <TextField
            fullWidth
            label="Invite Code"
            value={joinCode}
            onChange={e => setJoinCode(e.target.value)}
            placeholder="Enter the 8-character invite code"
            autoFocus
          />
          {joinResult && <Typography color="success.main" sx={{ mt: 2 }}>{joinResult}</Typography>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setOpenJoinDialog(false); setJoinResult(null); setJoinCode(''); }}>Cancel</Button>
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
                    <Button variant="contained" onClick={() => setOpenDialog(true)}>
                      <AddIcon sx={{ mr: 1 }} /> Create Your First Game
                    </Button>
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
                      <Tooltip title="Invite Code">
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

      {error && (
        <Alert severity="error" sx={{ mt: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {error}
        </Alert>
      )}
    </Container>
  );
}
