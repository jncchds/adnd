function NPCsTab({ npcs, npcsLoading, onOpenDialog, onDelete }: any) {
  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">NPCs</Typography>
        <Button variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
          Add NPC
        </Button>
      </Box>
      <Divider />
      {npcsLoading ? (
        <Typography sx={{ p: 2 }}>Loading...</Typography>
      ) : npcs.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No NPCs yet. Add one to get started.
        </Typography>
      ) : (
        <List>
          {npcs.map((npc: any) => (
            <ListItem key={npc.id} sx={{ px: 2, alignItems: 'flex-start' }}>
              <ListItemAvatar>
                <Avatar>🧙</Avatar>
              </ListItemAvatar>
              <ListItemText
                primary={npc.name}
                secondary={npc.description || 'No description'}
              />
              <IconButton size="small" onClick={() => onDelete(npc.id)} color="error">
                <DeleteIcon />
              </IconButton>
            </ListItem>
          ))}
        </List>
      )}
    </Paper>
  );
}

