import { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useGames } from '../api/hooks/useGame';
import { useLLMPresets } from '../api/hooks/useLLM';
import { useGameTemplates } from '../api/hooks/useTemplates';
import { api } from '../api/client';
import { Box, Typography, Paper, Chip, Button, useTheme, useMediaQuery } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import GameList from '../components/dashboard/GameList';
import CreateGameDialog from '../components/dashboard/CreateGameDialog';
import JoinGamePanel from '../components/dashboard/JoinGamePanel';

export default function DashboardPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const { games, isLoading, refetch, createGame, deleteGame, leaveGame, generateInvite, archiveGame, joinByCode } = useGames();
  const { presets, isLoading: presetsLoading } = useLLMPresets();
  const { templates, createTemplate, deleteTemplate } = useGameTemplates();

  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);

  useEffect(() => {
    if ((location.state as any)?.openCreate) {
      setShowCreateDialog(true);
      window.history.replaceState({}, '');
    }
  }, [location.state]);
  const [errorState, setErrorState] = useState<string | null>(null);
  const [successState, setSuccessState] = useState<string | null>(null);

  const handleOpenCreate = () => setShowCreateDialog(true);

  const handleJoin = async (code: string) => {
    setErrorState(null);
    setSuccessState(null);
    try {
      const result: any = await joinByCode(code);
      setSuccessState('Joined game! Navigating...');
      setTimeout(() => navigate(`/game/${result.gameId}`), 500);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleSaveTemplate = async (templateName: string, defaultName: string, systemId: string, llmPresetId: string | null, language: string, plotSeed: string, gameParameters: string) => {
    if (!templateName.trim()) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      await createTemplate({
        name: templateName,
        defaultName: defaultName || undefined,
        systemId,
        llmPresetId: llmPresetId || undefined,
        llmPresetName: presets?.find(p => p.id === llmPresetId)?.name,
        language,
        plotSeed: plotSeed || undefined,
        gameParameters: gameParameters || undefined,
      });
      setSuccessState('Template saved!');
      setTimeout(() => setSuccessState(null), 2000);
    } catch (e: any) {
      setErrorState(e.message);
      throw e;
    }
  };

  const handleCreate = async (name: string, systemId: string, llmPresetId: string | null, plotSeed: string, gameParameters: string, language: string) => {
    if (!name.trim()) return;
    setErrorState(null);
    setSuccessState(null);
    try {
      const result = await createGame(name, systemId, undefined, undefined, llmPresetId || undefined, plotSeed || undefined, gameParameters || undefined, language);
      setSuccessState('Game created! Starting AI-GM...');
      try { await api.startGame(result.id); }
      catch (startErr: any) {
        if (startErr.message?.includes('no LLM preset')) {
          setSuccessState('Game created! Please select an LLM preset in settings.');
        } else { throw startErr; }
      }
      setTimeout(() => navigate(`/game/${result.id}`), 500);
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

  const handleArchiveGame = async (gameId: string) => {
    setErrorState(null);
    try { await archiveGame(gameId); refetch(); }
    catch (e: any) { setErrorState(e.message); }
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

  const activeGames = games.filter(g => g.status !== 'Archived' && g.status !== 'Finished');

  return (
    <Box>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>Games</Typography>
      <JoinGamePanel onJoin={handleJoin} errorState={errorState} successState={successState} />
      {activeGames.length === 0 ? (
        <Paper elevation={2} sx={{ p: 6, textAlign: 'center', bgcolor: 'background.paper' }}>
          <Typography color="text.secondary" sx={{ mb: 2 }}>No games yet</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>Use the sidebar to create a new game or join one by code.</Typography>
          <Button variant="contained" onClick={handleOpenCreate} startIcon={<AddIcon />}>New Game</Button>
        </Paper>
      ) : (
        <>
          {!isMobile && <GameList games={activeGames} statusColor={statusColor} onCopyInvite={handleCopyInvite} onLeave={leaveGame} onArchive={handleArchiveGame} onDelete={deleteGame} />}
          {isMobile && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              {activeGames.map(game => (
                <Paper key={game.id} elevation={1} sx={{ p: 2 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography variant="h6" noWrap>{game.name}</Typography>
                      <Typography variant="body2" color="text.secondary">by {game.creatorName}</Typography>
                    </Box>
                    <Chip label={game.status} size="small" color={statusColor(game.status) as any} />
                  </Box>
                  <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mb: 1 }}>
                    <Chip label={game.systemId} size="small" variant="outlined" />
                    {game.language && <Chip label={`🌐 ${game.language}`} size="small" variant="outlined" color="info" />}
                    {game.llmPresetName && <Chip label={game.llmPresetName} size="small" variant="outlined" />}
                    {game.status === 'Active' && game.inviteCode && <Chip label={game.inviteCode} size="small" variant="outlined" color="primary" />}
                  </Box>
                  <Typography variant="caption" color="text.secondary">Created: {new Date(game.createdAt).toLocaleDateString()}</Typography>
                  <Box sx={{ display: 'flex', gap: 0.5, mt: 1.5, flexWrap: 'wrap' }}>
                    <Button size="small" variant="contained" component="a" href={`/game/${game.id}`}>Play</Button>
                    {game.status === 'Active' && game.inviteCode && <Button size="small" variant="outlined" onClick={() => handleCopyInvite(game.id)}>Copy Code</Button>}
                    {game.status === 'Active' && <Button size="small" variant="outlined" color="error" onClick={() => leaveGame(game.id)}>Leave</Button>}
                    <Button size="small" variant="outlined" onClick={() => handleArchiveGame(game.id)}>Archive</Button>
                    <Button size="small" variant="outlined" color="error" onClick={() => deleteGame(game.id)}>Delete</Button>
                  </Box>
                </Paper>
              ))}
            </Box>
          )}
        </>
      )}
      <CreateGameDialog
        open={showCreateDialog}
        onClose={() => setShowCreateDialog(false)}
        onCreate={handleCreate}
        presets={presets}
        presetsLoading={presetsLoading}
        templates={templates}
        selectedTemplateId={selectedTemplateId}
        onSelectedTemplateChange={setSelectedTemplateId}
        
        onDeleteTemplate={deleteTemplate}
        onSaveTemplate={handleSaveTemplate}
      />
    </Box>
  );
}
