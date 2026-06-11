import { Box, Typography, IconButton, ListItem, ListItemText, List, Button, Divider } from '@mui/material';
import { Delete as DeleteIcon, Add as AddIcon } from '@mui/icons-material';

export default function ConditionsTab({ conditions, setConditions, isEditMode, onOpenDialog }: any) {
  const conditionIcons: Record<string, string> = {
    'blinded': '👁️',
    'deafened': '👂',
    'frightened': '😨',
    'grappled': '🤝',
    'incapacitated': '🚫',
    'invisible': '👻',
    'paralyzed': '💎',
    'petrified': '🗿',
    'poisoned': '☠️',
    'prone': '🤸',
    'restrained': '🔗',
    'stunned': '💫',
    'unconscious': '😴',
  };

  if (conditions.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 4 }}>
        <Typography color="text.secondary" sx={{ mb: 2 }}>No active conditions.</Typography>
        {isEditMode && (
          <Button variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Condition
          </Button>
        )}
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="body2" color="text.secondary">
          {conditions.length} condition{conditions.length !== 1 ? 's' : ''}
        </Typography>
        {isEditMode && (
          <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
            Add Condition
          </Button>
        )}
      </Box>
      <Divider sx={{ mb: 2 }} />
      <List dense>
        {conditions.map((cond: any, i: number) => {
          const icon = conditionIcons[cond.name?.toLowerCase()] || '⚠️';
          return (
            <ListItem key={i} sx={{ px: 0, borderBottom: 1, borderColor: 'divider' }}>
              <ListItemText
                primary={
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Typography>{icon}</Typography>
                    <Typography variant="body2">{cond.name}</Typography>
                  </Box>
                }
                secondary={cond.description || ''}
              />
              {isEditMode && (
                <IconButton size="small" color="error" onClick={() => setConditions((prev: any) => prev.filter((_: any, idx: number) => idx !== i))}>
                  <DeleteIcon fontSize="small" />
                </IconButton>
              )}
            </ListItem>
          );
        })}
      </List>
    </Box>
  );
}
