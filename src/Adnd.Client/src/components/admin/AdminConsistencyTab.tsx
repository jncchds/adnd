function ConsistencyTab({ report, isLoading, onCheck }: any) {
  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Plot Consistency Check</Typography>
        <Button variant="contained" onClick={onCheck} disabled={isLoading}>
          {isLoading ? 'Checking...' : 'Check Consistency'}
        </Button>
      </Box>
      <Divider />
      {!report ? (
        <Typography sx={{ p: 4, textAlign: 'center', color: 'text.secondary' }}>
          Click "Check Consistency" to analyze recent game events for potential plot inconsistencies.
        </Typography>
      ) : (
        <Box sx={{ p: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Analyzed {report.messagesAnalyzed} messages · Checked at {new Date(report.checkedAt).toLocaleString()}
          </Typography>

          {report.warnings.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle1" color="warning.main">⚠️ Warnings ({report.warnings.length})</Typography>
              {report.warnings.map((w: string, i: number) => (
                <Alert severity="warning" variant="outlined" sx={{ mt: 1 }} key={i}>
                  <AlertTitle>Warning</AlertTitle>
                  {w}
                </Alert>
              ))}
            </Box>
          )}

          {report.findings.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle1" color="info.main">📋 Findings ({report.findings.length})</Typography>
              {report.findings.map((f: string, i: number) => (
                <Alert severity="info" variant="outlined" sx={{ mt: 1 }} key={i}>
                  <CheckCircleIcon fontSize="small" sx={{ mr: 1 }} />
                  {f}
                </Alert>
              ))}
            </Box>
          )}

          {report.warnings.length === 0 && report.findings.length === 0 && (
            <Alert severity="success" sx={{ mt: 2 }}>
              <AlertTitle>No Issues Found</AlertTitle>
              All recent events appear consistent. No plot contradictions detected.
            </Alert>
          )}
        </Box>
      )}
    </Paper>
  );
}

// ==================== Agent Calls Tab ====================

