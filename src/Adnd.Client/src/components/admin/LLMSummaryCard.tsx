interface SummaryCardProps {
  label: string;
  value: string;
  sublabel?: string;
  color?: 'primary' | 'success' | 'warning' | 'error';
  icon?: string;
}

function SummaryCard({ label, value, sublabel, color = 'primary', icon }: SummaryCardProps) {
  const colorMap = {
    primary: 'success',
    success: 'success',
    warning: 'warning',
    error: 'error',
  };

  return (
    <Card sx={{ height: '100%', bgcolor: 'background.paper', borderRadius: 2 }}>
      <CardContent sx={{ p: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
          {icon && <Typography variant="h4" sx={{ fontSize: '1.5rem' }}>{icon}</Typography>}
          <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>
            {label}
          </Typography>
        </Box>
        <Typography variant="h4" fontWeight={700} color={colorMap[color]}>
          {value}
        </Typography>
        {sublabel && (
          <Typography variant="caption" color="text.secondary">
            {sublabel}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

// ==================== Provider Row ====================

