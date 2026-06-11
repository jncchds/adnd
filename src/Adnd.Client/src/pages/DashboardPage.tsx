import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../api/hooks/useAuth';
import { useGames } from '../api/hooks/useGame';
import { useLLMPresets } from '../api/hooks/useLLM';
import { useGameTemplates } from '../api/hooks/useTemplates';
import { api } from '../api/client';
import {
  Box, Typography, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Alert, Chip, IconButton, Tooltip, AlertTitle, Button, Autocomplete,
  useMediaQuery, useTheme, CircularProgress
} from '@mui/material';
import { Delete as DeleteIcon, PlayArrow as PlayIcon, Archive as ArchiveIcon,
  Share as ShareIcon, ExitToApp as LeaveIcon, Add as AddIcon } from '@mui/icons-material';

export default function DashboardPage() {
  const { } = useAuth();
  const navigate = useNavigate();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm')); // < 600px
  const { games, isLoading, error, refetch, createGame, deleteGame, leaveGame, generateInvite, archiveGame, joinByCode } = useGames();
  const { presets, isLoading: presetsLoading } = useLLMPresets();
  const { templates, createTemplate, deleteTemplate } = useGameTemplates();
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [showJoinDialog, setShowJoinDialog] = useState(false);
  const [joinCode, setJoinCode] = useState('');
  const [gameName, setGameName] = useState('');
  const [systemId, setSystemId] = useState('dnd5e');
  const [llmPresetId, setLLMPresetId] = useState<string | null>(null);
  const [plotSeed, setPlotSeed] = useState('');
  const [gameParameters, setGameParameters] = useState('');
  const [language, setLanguage] = useState('English');
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);
  const [showTemplateDialog, setShowTemplateDialog] = useState(false);
  const [templateName, setTemplateName] = useState('');
  const [templateDefaultName, setTemplateDefaultName] = useState('');
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
    setLanguage('English');
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
      const result = await createGame(gameName, systemId, undefined, undefined, llmPresetId || undefined, plotSeed || undefined, gameParameters || undefined, language);
      handleCloseCreate();
      refetch();
      setSuccessState('Game created! Starting AI-GM...');
      // Auto-start the game so the LLM agent activates and generates plot threads
      try {
        await api.startGame(result.id);
      } catch (startErr: any) {
        // If start fails (e.g., no LLM preset), still navigate but show warning
        if (startErr.message?.includes('no LLM preset')) {
          setSuccessState('Game created! Please select an LLM preset in settings to start the AI-GM.');
        } else {
          throw startErr;
        }
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

  const activeGames = games.filter(g => g.status !== 'Archived' && g.status !== 'Finished');

  return (
    <Box>
      {/* Title */}
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>
        Games
      </Typography>

      {/* Join Game Panel */}
      <Paper elevation={1} sx={{ p: 3, mb: 3, borderRadius: 2, bgcolor: 'background.paper' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1.5 }}>
          <ShareIcon sx={{ color: 'primary.main' }} />
          <Typography variant="h6" sx={{ fontWeight: 600 }}>Join a Game</Typography>
        </Box>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Enter the invite code to join an existing game.
        </Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            fullWidth
            label="Invite Code"
            value={joinCode}
            onChange={e => setJoinCode(e.target.value)}
            placeholder="e.g., abc12345"
            sx={{ maxWidth: 300 }}
            onKeyDown={e => e.key === 'Enter' && handleJoin()}
          />
          <Button
            variant="contained"
            onClick={handleJoin}
            disabled={!joinCode.trim()}
            startIcon={<PlayIcon fontSize="small" />}
          >
            Join
          </Button>
        </Box>
        {errorState && (
          <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mt: 2 }}>
            <AlertTitle>Error</AlertTitle>
            {errorState}
          </Alert>
        )}
      </Paper>

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
          {/* Template Selector */}
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
            <Typography variant="subtitle2" color="text.secondary">Load from template:</Typography>
            <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', flex: 1 }}>
              {templates.length === 0 ? (
                <Chip label="No templates yet" size="small" variant="outlined" color="default" />
              ) : (
                templates.map(t => (
                  <Chip
                    key={t.id}
                    label={t.name}
                    size="small"
                    clickable
                    onClick={() => {
                      setSelectedTemplateId(t.id);
                      setSystemId(t.systemId);
                      setLLMPresetId(t.llmPresetId || null);
                      setLanguage(t.language);
                      setPlotSeed(t.plotSeed || '');
                      setGameParameters(t.gameParameters || '');
                      setGameName(t.defaultName || '');
                    }}
                    sx={{
                      bgcolor: selectedTemplateId === t.id ? 'primary.lighter' : 'background.default',
                      borderColor: selectedTemplateId === t.id ? 'primary.main' : 'divider',
                      borderWidth: 1,
                      borderStyle: 'solid',
                    }}
                    onDelete={() => {
                      deleteTemplate(t.id);
                      if (selectedTemplateId === t.id) setSelectedTemplateId(null);
                    }}
                  />
                ))
              )}
              <Chip
                label="+ New Template"
                size="small"
                clickable
                onClick={() => {
                  setShowTemplateDialog(true);
                  setTemplateName('');
                  setTemplateDefaultName('');
                }}
              />
            </Box>
          </Box>
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
          <Box sx={{ mb: 2 }}>
            <TextField
              fullWidth
              select
              label="Narration Language"
              value={language}
              onChange={e => setLanguage(e.target.value)}
              SelectProps={{ native: true }}
            >
              <option value="English">English</option>
              <option value="Spanish">Español (Spanish)</option>
              <option value="French">Français (French)</option>
              <option value="German">Deutsch (German)</option>
              <option value="Italian">Italiano (Italian)</option>
              <option value="Portuguese">Português (Portuguese)</option>
              <option value="Japanese">日本語 (Japanese)</option>
              <option value="Korean">한국어 (Korean)</option>
              <option value="Chinese">中文 (Chinese)</option>
              <option value="Ukrainian">Українська (Ukrainian)</option>
              <option value="Polish">Polski (Polish)</option>
              <option value="Dutch">Nederlands (Dutch)</option>
              <option value="Swedish">Svenska (Swedish)</option>
              <option value="Norwegian">Norsk (Norwegian)</option>
              <option value="Finnish">Suomi (Finnish)</option>
              <option value="Danish">Dansk (Danish)</option>
              <option value="Greek">Ελληνικά (Greek)</option>
              <option value="Turkish">Türkçe (Turkish)</option>
              <option value="Arabic">العربية (Arabic)</option>
              <option value="Hindi">हिन्दी (Hindi)</option>
            </TextField>
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mt: 1 }}>
              <Typography variant="caption" color="text.secondary">Or type a custom language:</Typography>
            </Box>
            <TextField
              fullWidth
              size="small"
              placeholder="e.g., Esperanto, Klingon, etc."
              value={language}
              onChange={e => setLanguage(e.target.value)}
              sx={{ mt: 0.5 }}
            />
          </Box>
          <Autocomplete
            options={presets || []}
            value={presets?.find(p => p.id === llmPresetId) || null}
            onChange={(_event, newValue) => {
              setLLMPresetId(newValue?.id || null);
            }}
            getOptionLabel={(option) => option.name}
            isOptionEqualToValue={(option, value) => option.id === value.id}
            disabled={presetsLoading || presets?.length === 0}
            renderInput={(params) => (
              <TextField
                {...params}
                fullWidth
                label="LLM Preset (for AI-GM)"
                placeholder={presetsLoading ? 'Loading presets...' : presets?.length === 0 ? 'No presets available — create one first' : 'Select an LLM preset'}
                InputProps={{
                  ...params.InputProps,
                  endAdornment: (
                    <>
                      {presetsLoading ? <CircularProgress color="inherit" size={20} /> : null}
                      {params.InputProps?.endAdornment}
                    </>
                  ),
                }}
              />
            )}
            renderOption={(props, option) => (
              <Box component="li" {...props} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Typography variant="body2">{option.name}</Typography>
                <Chip label={option.providerType} size="small" variant="outlined" sx={{ height: 16, fontSize: 10 }} />
              </Box>
            )}
          />
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
          <Button onClick={() => setShowTemplateDialog(true)} variant="outlined" disabled={!gameName.trim()}>
            💾 Save as Template
          </Button>
          <Button onClick={handleCreate} variant="contained" disabled={!gameName.trim()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>

      {/* Save Template Dialog */}
      <Dialog open={showTemplateDialog} onClose={() => setShowTemplateDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Save as Template</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Save your current game configuration as a reusable template.
          </Typography>
          <TextField
            fullWidth
            label="Template Name"
            value={templateName}
            onChange={e => setTemplateName(e.target.value)}
            placeholder="e.g., D&D Fantasy Adventure"
            helperText="A short name to identify this template"
          />
          <TextField
            fullWidth
            label="Default Game Name"
            value={templateDefaultName}
            onChange={e => setTemplateDefaultName(e.target.value)}
            placeholder="e.g., My New Adventure"
            helperText="Pre-filled game name (optional)"
          />
          <Box sx={{ bgcolor: 'background.default', p: 1.5, borderRadius: 1 }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Will save:</Typography>
            <Typography variant="caption" sx={{ display: 'block' }}>🎮 System: {systemId}</Typography>
            {llmPresetId && <Typography variant="caption" sx={{ display: 'block' }}>🤖 LLM Preset: {presets?.find(p => p.id === llmPresetId)?.name}</Typography>}
            <Typography variant="caption" sx={{ display: 'block' }}>🌐 Language: {language}</Typography>
            {plotSeed && <Typography variant="caption" sx={{ display: 'block' }}>📖 Plot Seed: {plotSeed.substring(0, 60)}{plotSeed.length > 60 ? '...' : ''}</Typography>}
            {gameParameters && <Typography variant="caption" sx={{ display: 'block' }}>⚙️ Parameters: {gameParameters.substring(0, 60)}{gameParameters.length > 60 ? '...' : ''}</Typography>}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowTemplateDialog(false)}>Cancel</Button>
          <Button
            onClick={async () => {
              if (!templateName.trim()) return;
              try {
                await createTemplate({
                  name: templateName,
                  defaultName: templateDefaultName || undefined,
                  systemId,
                  llmPresetId: llmPresetId || undefined,
                  llmPresetName: presets?.find(p => p.id === llmPresetId)?.name,
                  language,
                  plotSeed: plotSeed || undefined,
                  gameParameters: gameParameters || undefined,
                });
                setShowTemplateDialog(false);
                setTemplateName('');
                setTemplateDefaultName('');
                setSuccessState('Template saved!');
                setTimeout(() => setSuccessState(null), 2000);
              } catch (e: any) {
                setErrorState(e.message);
              }
            }}
            variant="contained"
            disabled={!templateName.trim()}
          >
            Save Template
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

      {/* Games List */}
      {activeGames.length === 0 ? (
        <Paper elevation={2} sx={{ p: 6, textAlign: 'center', bgcolor: 'background.paper' }}>
          <Typography color="text.secondary" sx={{ mb: 2 }}>No games yet</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Use the sidebar to create a new game or join one by code.
          </Typography>
          <Button variant="contained" onClick={handleOpenCreate} startIcon={<AddIcon />}>New Game</Button>
        </Paper>
      ) : (
        <>
          {/* Desktop: Table layout */}
          {!isMobile && (
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
                    {activeGames.map(game => (
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
                            <IconButton component="a" href={`/game/${game.id}`} size="small">
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
                          {game.status === 'Active' && (
                            <Tooltip title="Leave">
                              <IconButton onClick={() => leaveGame(game.id)} size="small" color="error">
                                <LeaveIcon />
                              </IconButton>
                            </Tooltip>
                          )}
                          <Tooltip title="Archive">
                            <IconButton size="small" color="inherit" onClick={() => handleArchiveGame(game.id)}>
                              <ArchiveIcon />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Delete">
                            <IconButton onClick={() => deleteGame(game.id)} size="small" color="error">
                              <DeleteIcon />
                            </IconButton>
                          </Tooltip>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>
          )}

          {/* Mobile: Card layout */}
          {isMobile && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              {activeGames.map(game => (
                <Paper key={game.id} elevation={1} sx={{ p: 2 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography variant="h6" noWrap>{game.name}</Typography>
                      <Typography variant="body2" color="text.secondary">
                        by {game.creatorName}
                      </Typography>
                    </Box>
                    <Chip label={game.status} size="small" color={statusColor(game.status) as any} />
                  </Box>
                  <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mb: 1 }}>
                    <Chip label={game.systemId} size="small" variant="outlined" />
                    {game.language && <Chip label={`🌐 ${game.language}`} size="small" variant="outlined" color="info" />}
                    {game.llmPresetName && <Chip label={game.llmPresetName} size="small" variant="outlined" />}
                    {game.status === 'Active' && game.inviteCode && (
                      <Chip label={game.inviteCode} size="small" variant="outlined" color="primary" />
                    )}
                  </Box>
                  <Typography variant="caption" color="text.secondary">
                    Created: {new Date(game.createdAt).toLocaleDateString()}
                  </Typography>
                  <Box sx={{ display: 'flex', gap: 0.5, mt: 1.5, flexWrap: 'wrap' }}>
                    <Button size="small" variant="contained" component="a" href={`/game/${game.id}`} startIcon={<PlayIcon />}>Play</Button>
                    {game.status === 'Active' && game.inviteCode && (
                      <Button size="small" variant="outlined" onClick={() => handleCopyInvite(game.id)} startIcon={<ShareIcon />}>Copy Code</Button>
                    )}
                    {game.status === 'Active' && (
                      <Button size="small" variant="outlined" color="error" onClick={() => leaveGame(game.id)} startIcon={<LeaveIcon />}>Leave</Button>
                    )}
                    <Button size="small" variant="outlined" onClick={() => handleArchiveGame(game.id)} startIcon={<ArchiveIcon />}>Archive</Button>
                    <Button size="small" variant="outlined" color="error" onClick={() => deleteGame(game.id)} startIcon={<DeleteIcon />}>Delete</Button>
                  </Box>
                </Paper>
              ))}
            </Box>
          )}
        </>
      )}
    </Box>
  );
}
