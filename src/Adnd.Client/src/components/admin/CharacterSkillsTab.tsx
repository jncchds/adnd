import { Box, Typography, Chip, TextField, ListItem, ListItemText, List } from '@mui/material';

export default function SkillsTab({ skills, setSkills: _setSkills, proficiency, setProficiency, isEditMode }: any) {
  void _setSkills; // used when skills become editable
  const skillNames = Object.keys(skills).sort();

  if (skillNames.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ textAlign: 'center', py: 4 }}>
        No skills defined.
      </Typography>
    );
  }

  return (
    <List>
      {skillNames.map(name => {
        const val = skills[name] || 0;
        const displayVal = val;
        const isProficient = val > 0 && val > (skills[name]?.base || 0);
        return (
          <ListItem key={name} sx={{ px: 0, borderBottom: 1, borderColor: 'divider' }}>
            <ListItemText
              primary={name}
              secondary={
                isProficient
                  ? `Proficient (+${proficiency})`
                  : 'Untrained'
              }
            />
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              {isProficient && <Chip label="Prof" size="small" color="primary" variant="outlined" />}
              <Typography variant="h6" sx={{ minWidth: 30, textAlign: 'right' }}>
                {displayVal >= 0 ? `+${displayVal}` : displayVal}
              </Typography>
            </Box>
          </ListItem>
        );
      })}
      {isEditMode && (
        <ListItem sx={{ px: 0, bgcolor: 'action.hover' }}>
          <ListItemText
            primary="Proficiency Bonus"
            secondary="Global modifier for proficient skills"
          />
          <TextField
            size="small"
            type="number"
            value={proficiency}
            onChange={e => setProficiency(parseInt(e.target.value) || 0)}
            sx={{ width: 80 }}
            inputProps={{ min: 0, max: 10 }}
          />
        </ListItem>
      )}
    </List>
  );
}

