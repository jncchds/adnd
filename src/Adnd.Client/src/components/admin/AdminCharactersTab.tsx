function CharactersTab({ characters }: { characters: any[] }) {
  const navigate = useNavigate();
  return (
    <Paper>
      <Box sx={{ p: 2 }}>
        <Typography variant="h6">Characters ({characters.length})</Typography>
      </Box>
      <Divider />
      {characters.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No characters yet.
        </Typography>
      ) : (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 2, p: 2 }}>
          {characters.map((c: any) => (
            <Card key={c.id}>
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                  <Typography variant="h6">{c.name}</Typography>
                  <Chip label={`${c.class} Lv.${c.level}`} size="small" />
                </Box>
                <Typography variant="body2" color="text.secondary">
                  Player: {c.playerName}
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, mt: 1 }}>
                  <Chip label={`HP: ${c.currentHP}/${c.maxHP}`} size="small" color={c.currentHP < c.maxHP * 0.3 ? 'error' : 'default'} />
                </Box>
                <Box sx={{ mt: 1.5 }}>
                  <Button size="small" variant="outlined" fullWidth startIcon={<SheetIcon />}
                    onClick={() => navigate(`/character/${c.id}`)}>
                    View Sheet
                  </Button>
                </Box>
              </CardContent>
            </Card>
          ))}
        </Box>
      )}
    </Paper>
  );
}

