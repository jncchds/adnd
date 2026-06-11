import { Box, Typography, Chip, IconButton, ListItem, ListItemText, List, Button, Divider } from '@mui/material';
import { Delete as DeleteIcon, Add as AddIcon } from '@mui/icons-material';
import SpellSlotTracker from './SpellSlotTracker';

export default function SpellsTab({ spells, setSpells, isEditMode, onOpenDialog, spellSlots, onSlotChange }: any) {
  const spellLevels = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

  const groupedSpells = spellLevels.reduce((acc: any, level) => {
    const levelSpells = spells.filter((s: any) => s.level === level);
    if (levelSpells.length > 0) {
      acc[level] = levelSpells;
    }
    return acc;
  }, {});

  const totalSpells = spells.length;
  const preparedSpells = spells.filter((s: any) => s.prepared).length;

  if (totalSpells === 0 && (!spellSlots || Object.keys(spellSlots).length === 0)) {
    return (
      <Box sx={{ textAlign: 'center', py: 4 }}>
        <Typography color="text.secondary" sx={{ mb: 2 }}>No spells or spell slots defined.</Typography>
        {isEditMode && (
          <Button variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Spell
          </Button>
        )}
      </Box>
    );
  }

  return (
    <Box>
      {spellSlots && Object.keys(spellSlots).length > 0 && (
        <SpellSlotTracker
          spellSlots={spellSlots}
          onSlotChange={onSlotChange}
          canEdit={isEditMode}
        />
      )}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="body2" color="text.secondary">
          {preparedSpells} / {totalSpells} spells prepared
        </Typography>
        {isEditMode && (
          <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Spell
          </Button>
        )}
      </Box>
      <Divider sx={{ mb: 2 }} />
      {spellLevels.filter(l => groupedSpells[l]).map(level => (
        <Box key={level} sx={{ mb: 2 }}>
          <Typography variant="subtitle2" color="primary" sx={{ mb: 1 }}>
            {level === 0 ? 'Cantrips' : `${level}st Level`}
          </Typography>
          <List dense>
            {groupedSpells[level].map((spell: any, i: number) => (
              <ListItem key={i} sx={{ px: 0, borderBottom: 1, borderColor: 'divider' }}>
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      {spell.prepared && <Chip label="✓" size="small" color="success" variant="outlined" />}
                      <Typography variant="body2">{spell.name}</Typography>
                    </Box>
                  }
                  secondary={spell.description || 'No description'}
                />
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  {spell.slotsUsed > 0 && (
                    <Chip label={`${spell.slotsUsed} slot${spell.slotsUsed > 1 ? 's' : ''}`} size="small" color="warning" variant="outlined" />
                  )}
                  {isEditMode && (
                    <IconButton size="small" color="error" onClick={() => setSpells((prev: any) => prev.filter((_: any, idx: number) => idx !== i))}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  )}
                </Box>
              </ListItem>
            ))}
          </List>
        </Box>
      ))}
    </Box>
  );
}

