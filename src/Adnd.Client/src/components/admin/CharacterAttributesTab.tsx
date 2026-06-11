import { Box, Typography, Paper, IconButton, Chip } from '@mui/material';

export default function AttributesTab({ attributes, setAttributes, isEditMode }: any) {
  const attrNames = Object.keys(attributes).sort();

  if (attrNames.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ textAlign: 'center', py: 4 }}>
        No attributes defined. Click "Edit" to add attributes.
      </Typography>
    );
  }

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
      {attrNames.map(attr => {
        const value = attributes[attr] || 10;
        const mod = Math.floor((value - 10) / 2);
        return (
          <Box key={attr} sx={{ flex: '1 1 200px' }}>
            <Paper sx={{ p: 2, textAlign: 'center' }}>
              <Typography variant="subtitle1" textTransform="capitalize" sx={{ mb: 1 }}>
                {attr.replace(/([A-Z])/g, ' $1').trim()}
              </Typography>
              {isEditMode ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', gap: 1, alignItems: 'center' }}>
                  <IconButton size="small" onClick={() => setAttributes((prev: Record<string, number>) => ({ ...prev, [attr]: Math.max(1, (prev[attr] || 10) - 1) }))}>
                    -
                  </IconButton>
                  <Typography variant="h4">{value}</Typography>
                  <IconButton size="small" onClick={() => setAttributes((prev: Record<string, number>) => ({ ...prev, [attr]: (prev[attr] || 10) + 1 }))}>
                    +
                  </IconButton>
                </Box>
              ) : (
                <Typography variant="h4">{value}</Typography>
              )}
              <Chip
                label={mod >= 0 ? `+${mod}` : `${mod}`}
                size="small"
                sx={{ mt: 1, fontWeight: 'bold', fontSize: '1.1rem' }}
                color={mod >= 3 ? 'success' : mod <= -3 ? 'error' : 'default'}
              />
            </Paper>
          </Box>
        );
      })}
    </Box>
  );
}

