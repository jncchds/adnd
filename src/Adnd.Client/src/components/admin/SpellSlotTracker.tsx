function SpellSlotTracker({ spellSlots, onSlotChange, canEdit }: any) {
  const spellLevels = [1, 2, 3, 4, 5, 6, 7, 8, 9];

  const handleSlotChange = (level: number, type: 'total' | 'remaining', value: number) => {
    if (!spellSlots) return;
    const current = spellSlots[level] || { total: 0, remaining: 0 };
    const updated = { ...current, [type]: Math.max(0, Math.min(type === 'total' ? current.total : current.total, value)) };
    if (type === 'remaining' && updated.remaining > updated.total) {
      updated.remaining = updated.total;
    }
    onSlotChange(level, updated);
  };

  const hasSlots = spellLevels.some(l => (spellSlots?.[l]?.total || 0) > 0);

  if (!hasSlots) return null;

  return (
    <Paper sx={{ p: 2, mb: 2, bgcolor: 'background.default', border: '1px solid', borderColor: 'divider' }}>
      <Typography variant="subtitle2" gutterBottom sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
        📜 Spell Slots
      </Typography>
      <Grid container spacing={1}>
        {spellLevels.filter(l => (spellSlots?.[l]?.total || 0) > 0).map(level => (
          <Grid size={{ xs: 4, sm: 3, md: 2 }} key={level}>
            <Box sx={{ textAlign: 'center' }}>
              <Typography variant="caption" color="text.secondary">
                {level === 1 ? `${level}st` : level === 2 ? `${level}nd` : level === 3 ? `${level}rd` : `${level}th`}
              </Typography>
              {canEdit ? (
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 0.5, my: 0.5 }}>
                  <IconButton
                    size="small"
                    disabled={(spellSlots?.[level]?.remaining || 0) <= 0}
                    onClick={() => handleSlotChange(level, 'remaining', (spellSlots[level]?.remaining || 0) - 1)}
                  >
                    <RemoveIcon fontSize="small" />
                  </IconButton>
                  <Typography
                    variant="h6"
                    sx={{
                      color: (spellSlots?.[level]?.remaining || 0) === 0 ? 'error.main' :
                             (spellSlots?.[level]?.remaining || 0) < (spellSlots?.[level]?.total || 0) ? 'warning.main' :
                             'success.main',
                      minWidth: 24,
                    }}
                  >
                    {spellSlots[level]?.remaining || 0}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">/</Typography>
                  <Typography
                    variant="h6"
                    sx={{ minWidth: 24 }}
                  >
                    {spellSlots[level]?.total || 0}
                  </Typography>
                  <IconButton
                    size="small"
                    disabled={(spellSlots?.[level]?.remaining || 0) >= (spellSlots?.[level]?.total || 0)}
                    onClick={() => handleSlotChange(level, 'remaining', (spellSlots[level]?.remaining || 0) + 1)}
                  >
                    <AddIcon fontSize="small" />
                  </IconButton>
                </Box>
              ) : (
                <Typography
                  variant="h6"
                  sx={{
                    color: (spellSlots?.[level]?.remaining || 0) === 0 ? 'error.main' :
                           (spellSlots?.[level]?.remaining || 0) < (spellSlots?.[level]?.total || 0) ? 'warning.main' :
                           'success.main',
                  }}
                >
                  {spellSlots[level]?.remaining || 0}/{spellSlots[level]?.total || 0}
                </Typography>
              )}
            </Box>
          </Grid>
        ))}
      </Grid>
      {canEdit && (
        <Box sx={{ mt: 1, display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
          <Button
            size="small"
            variant="outlined"
            color="success"
            onClick={() => {
              if (!spellSlots) return;
              const refreshed: any = { ...spellSlots };
              spellLevels.forEach(l => {
                if (spellSlots[l]) refreshed[l] = { total: spellSlots[l].total, remaining: spellSlots[l].total };
              });
              onSlotChange('_refresh', refreshed);
            }}
          >
            Refresh All
          </Button>
        </Box>
      )}
    </Paper>
  );
}

