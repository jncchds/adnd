import { useState, useEffect } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions,
  Button, TextField, Typography, Box, Chip, Alert,
} from '@mui/material';
import { SportsEsports as DiceIcon } from '@mui/icons-material';

interface PlayerRollDialogProps {
  open: boolean;
  onClose: () => void;
  skill: string;
  formula: string;
  dc: number;
  context: string;
  optional: boolean;
  onRoll: (formula: string) => Promise<void>;
}

export default function PlayerRollDialog({
  open, onClose, skill, formula, dc, context, optional, onRoll,
}: PlayerRollDialogProps) {
  const [inputFormula, setInputFormula] = useState(formula);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setInputFormula(formula);
    setError(null);
  }, [formula, open]);

  const handleRoll = async () => {
    try {
      await onRoll(inputFormula);
      onClose();
    } catch (e: any) {
      setError(e.message);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        🎲 Roll Requested
      </DialogTitle>
      <DialogContent sx={{ mt: 1 }}>
        <Box sx={{ mb: 2 }}>
          {skill && (
            <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 0.5 }}>
              Skill: {skill}
            </Typography>
          )}
          {context && (
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
              {context}
            </Typography>
          )}
          <Typography variant="body2">
            DC: <strong>{dc}</strong>
            {optional && <Chip label="Optional" size="small" color="warning" sx={{ ml: 1 }} />}
          </Typography>
        </Box>

        <TextField
          fullWidth
          label="Dice Formula"
          value={inputFormula}
          onChange={e => setInputFormula(e.target.value)}
          placeholder="e.g., 1d20, 2d6+3, 4d6kh3"
          sx={{ mb: 2 }}
          InputProps={{
            startAdornment: (
              <Box sx={{ mr: 1, display: 'flex', alignItems: 'center' }}>
                <DiceIcon color="action" fontSize="small" />
              </Box>
            ),
          }}
        />

        <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mb: 1 }}>
          {['1d4', '1d6', '1d8', '1d10', '1d12', '1d20', '2d6', '3d6', '4d6kh3'].map(d => (
            <Chip
              key={d}
              label={d}
              clickable
              onClick={() => setInputFormula(d)}
              size="small"
              sx={{ fontSize: 10 }}
            />
          ))}
        </Box>

        {error && (
          <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          onClick={handleRoll}
          variant="contained"
          startIcon={<DiceIcon />}
          disabled={!inputFormula.trim()}
        >
          Roll {inputFormula}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
