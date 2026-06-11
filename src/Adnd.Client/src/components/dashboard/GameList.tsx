import { Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Chip, Typography, IconButton, Tooltip } from '@mui/material';
import { PlayArrow as PlayIcon, Share as ShareIcon, ExitToApp as LeaveIcon, Archive as ArchiveIcon, Delete as DeleteIcon } from '@mui/icons-material';

interface GameListProps {
  games: any[];
  statusColor: (status: string) => string;
  
  onCopyInvite: (gameId: string) => void;
  onLeave: (gameId: string) => void;
  onArchive: (gameId: string) => void;
  onDelete: (gameId: string) => void;
}

export default function GameList({ games, statusColor, onCopyInvite, onLeave, onArchive, onDelete }: GameListProps) {
  if (games.length === 0) return null;

  return (
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
            {games.map(game => (
              <TableRow key={game.id} hover>
                <TableCell>
                  <Typography variant="body1">{game.name}</Typography>
                  <Typography variant="body2" color="text.secondary">by {game.creatorName}</Typography>
                  {game.llmPresetName && (
                    <Typography variant="caption" color="primary">LLM: {game.llmPresetName}</Typography>
                  )}
                  {game.status === 'Active' && game.inviteCode && (
                    <Typography variant="caption" color="text.secondary">Code: <strong>{game.inviteCode}</strong></Typography>
                  )}
                </TableCell>
                <TableCell><Chip label={game.systemId} size="small" variant="outlined" /></TableCell>
                <TableCell><Chip label={game.status} size="small" color={statusColor(game.status) as any} /></TableCell>
                <TableCell><Typography variant="body2" color="text.secondary">{new Date(game.createdAt).toLocaleDateString()}</Typography></TableCell>
                <TableCell align="right">
                  <Tooltip title="Play">
                    <IconButton component="a" href={`/game/${game.id}`} size="small"><PlayIcon /></IconButton>
                  </Tooltip>
                  {game.status === 'Active' && game.inviteCode && (
                    <Tooltip title={game.inviteCode}>
                      <span><Chip label={game.inviteCode} size="small" variant="outlined" sx={{ mr: 0.5 }} /></span>
                    </Tooltip>
                  )}
                  <Tooltip title="Copy Invite Code">
                    <IconButton onClick={() => onCopyInvite(game.id)} size="small"><ShareIcon /></IconButton>
                  </Tooltip>
                  {game.status === 'Active' && (
                    <Tooltip title="Leave">
                      <IconButton onClick={() => onLeave(game.id)} size="small" color="error"><LeaveIcon /></IconButton>
                    </Tooltip>
                  )}
                  <Tooltip title="Archive">
                    <IconButton size="small" color="inherit" onClick={() => onArchive(game.id)}><ArchiveIcon /></IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <IconButton onClick={() => onDelete(game.id)} size="small" color="error"><DeleteIcon /></IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}
