function OverviewTab({ editName, setEditName, editClass, setEditClass, editLevel, setEditLevel, editMaxHP, editCurrentHP, setEditCurrentHP, isEditMode, onSave, onCancel }: any) {
  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
      <Box sx={{ flex: '1 1 300px' }}>
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle1" color="primary" gutterBottom>Character Info</Typography>
          {isEditMode ? (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
              <TextField fullWidth label="Name" value={editName} onChange={e => setEditName(e.target.value)} size="small" />
              <TextField fullWidth select label="Class" value={editClass} onChange={e => setEditClass(e.target.value)} size="small">
                {['Fighter', 'Wizard', 'Rogue', 'Cleric', 'Ranger', 'Barbarian', 'Bard', 'Druid', 'Monk', 'Paladin', 'Sorcerer', 'Warlock'].map(c => (
                  <MenuItem key={c} value={c}>{c}</MenuItem>
                ))}
              </TextField>
              <Box sx={{ display: 'flex', gap: 1 }}>
                <TextField fullWidth size="small" label="Level" type="number" value={editLevel} onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEditLevel(parseInt(e.target.value) || 1)} inputProps={{ min: 1 }} />
                <Button size="small" variant="outlined" onClick={() => setEditLevel((l: number) => l + 1)}>+1</Button>
              </Box>
            </Box>
          ) : (
            <Box>
              <Typography variant="body2">Class: <strong>{editClass}</strong></Typography>
              <Typography variant="body2">Level: <strong>{editLevel}</strong></Typography>
            </Box>
          )}
        </Paper>
      </Box>
      <Box sx={{ flex: '1 1 300px' }}>
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle1" color="primary" gutterBottom>Quick Actions</Typography>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            <Button size="small" variant="outlined" onClick={onSave} disabled={!isEditMode}>
              Save Changes
            </Button>
            <Button size="small" variant="outlined" onClick={onCancel} disabled={!isEditMode}>
              Cancel
            </Button>
          </Box>
        </Paper>
      </Box>
      <Box sx={{ width: '100%' }}>
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle1" color="primary" gutterBottom>HP Quick Adjust</Typography>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
            <Typography variant="body2">Current: {editCurrentHP} / {editMaxHP}</Typography>
            {isEditMode && (
              <>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP((prev: number) => Math.max(0, prev - 1))}>-1</Button>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP((prev: number) => Math.max(0, prev - 5))}>-5</Button>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP((prev: number) => prev + 1)}>+1</Button>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP((prev: number) => prev + 5)}>+5</Button>
                <Button size="small" variant="outlined" onClick={() => setEditCurrentHP(editMaxHP)}>Full Heal</Button>
                <Button size="small" variant="outlined" color="error" onClick={() => setEditCurrentHP(0)}>Die</Button>
              </>
            )}
          </Box>
        </Paper>
      </Box>
    </Box>
  );
}

