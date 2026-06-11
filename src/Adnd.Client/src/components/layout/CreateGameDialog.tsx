import { useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Button, Alert, AlertTitle,
} from '@mui/material';

interface CreateGameDialogProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (name: string, systemId: string, llmPresetId: string | null, plotSeed: string, gameParameters: string) => void;
  presets: { id: string; name: string; providerType: string }[];
  error: string | null;
  success: string | null;
}

export default function CreateGameDialog({ open, onClose, onSubmit, presets, error, success }: CreateGameDialogProps) {
  const [gameName, setGameName] = useState('');
  const [systemId, setSystemId] = useState('dnd5e');
  const [llmPresetId, setLLMPresetId] = useState<string | null>(null);
  const [plotSeed, setPlotSeed] = useState('');
  const [gameParameters, setGameParameters] = useState('');

  const handleSubmit = () => {
    if (!gameName.trim()) return;
    onSubmit(gameName, systemId, llmPresetId, plotSeed, gameParameters);
    setGameName('');
    setSystemId('dnd5e');
    setLLMPresetId(null);
    setPlotSeed('');
    setGameParameters('');
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Create New Game</DialogTitle>
      <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <TextField fullWidth label="Game Name" value={gameName} onChange={e => setGameName(e.target.value)} autoFocus />
        <TextField fullWidth select label="System" value={systemId} onChange={e => setSystemId(e.target.value)}>
          <option value="dnd5e">D&D 5th Edition</option>
          <option value="pf2e">Pathfinder 2nd Edition</option>
          <option value="coc7e">Call of Cthulhu 7th Edition</option>
        </TextField>
        <TextField fullWidth select label="LLM Preset (for AI-GM)" value={llmPresetId || ''} onChange={e => setLLMPresetId(e.target.value || null)}>
          <option value="">None</option>
          {presets.map(p => <option key={p.id} value={p.id}>{p.name} ({p.providerType})</option>)}
        </TextField>
        <TextField fullWidth multiline rows={3} label="Plot Seed (initial story premise)" value={plotSeed} onChange={e => setPlotSeed(e.target.value)} placeholder="Describe the initial story, setting, and tone..." />
        <TextField fullWidth multiline rows={2} label="Game Parameters (tone, difficulty, pacing)" value={gameParameters} onChange={e => setGameParameters(e.target.value)} placeholder="e.g., Dark tone, medium difficulty, fast-paced..." />
        {error && <Alert severity="error" onClose={() => {}}><AlertTitle>Error</AlertTitle>{error}</Alert>}
        {success && <Alert severity="success" onClose={() => {}}>{success}</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSubmit} variant="contained" disabled={!gameName.trim()}>Create</Button>
      </DialogActions>
    </Dialog>
  );
}
