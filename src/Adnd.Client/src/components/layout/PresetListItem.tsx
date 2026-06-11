import { ListItemButton, ListItemIcon, ListItemText } from '@mui/material';
import { AutoAwesome as LLMIcon } from '@mui/icons-material';
import type { LLMPreset } from '../../types/llm.types';

interface PresetListItemProps {
  preset: LLMPreset;
  drawerOpen: boolean;
  onSelect: (id: string) => void;
  onAdd: () => void;
}

export default function PresetListItem({ preset, drawerOpen, onSelect, onAdd }: PresetListItemProps) {
  const buttonBaseSx = {
    width: '100%', justifyContent: 'flex-start', pl: 2, borderRadius: 1, mb: 0.5,
    minHeight: 40, px: 1.5, bgcolor: 'transparent',
    '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
  };

  return (
    <>
      <ListItemButton
        onClick={() => onSelect(preset.id)}
        sx={{
          ...buttonBaseSx,
          bgcolor: preset.isDefault ? 'rgba(145,71,255,0.08)' : 'transparent',
          border: preset.isDefault ? '1px solid' : 'none',
          borderColor: preset.isDefault ? 'rgba(145,71,255,0.3)' : 'transparent',
        }}
      >
        <ListItemIcon sx={{ minWidth: 0, mr: 'auto', justifyContent: 'center' }}>
          <LLMIcon fontSize="small" color="action" />
        </ListItemIcon>
        {drawerOpen && (
          <ListItemText
            primary={preset.name}
            primaryTypographyProps={{ noWrap: true, fontSize: 13, fontWeight: 500 }}
          />
        )}
      </ListItemButton>
      {drawerOpen && (
        <ListItemButton
          onClick={onAdd}
          sx={{
            ...buttonBaseSx,
            bgcolor: 'rgba(145,71,255,0.1)', color: 'primary.light',
            '&:hover': { bgcolor: 'rgba(145,71,255,0.15)' }, mt: 1,
          }}
        >
          <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
            <LLMIcon fontSize="small" color="primary" />
          </ListItemIcon>
          <ListItemText primary="Add Preset" />
        </ListItemButton>
      )}
    </>
  );
}
