function LLMLogsTab({ logs, isLoading, onRefresh, onOpenDetail, onDelete, filterProvider, onFilterProviderChange, filterFrom, onFilterFromChange, filterTo, onFilterToChange }: any) {
  const getStatusColor = (success: boolean) => success ? 'success' : 'error';

  const getProviderIcon = (provider: string) => {
    switch (provider) {
      case 'ollama': return '🦙';
      case 'lmstudio': return '🏠';
      case 'openai': return '🔵';
      case 'google': return '🟢';
      default: return '🤖';
    }
  };

  const formatTokens = (tokens: number | undefined) => {
    if (!tokens) return '—';
    if (tokens >= 1000) return `${(tokens / 1000).toFixed(1)}k`;
    return tokens.toString();
  };

  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h6">LLM Interactions ({logs.length})</Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          <Select size="small" value={filterProvider} onChange={e => onFilterProviderChange(e.target.value)} sx={{ minWidth: 120 }}>
            <MenuItem value="">All Providers</MenuItem>
            <MenuItem value="ollama">Ollama</MenuItem>
            <MenuItem value="lmstudio">LM Studio</MenuItem>
            <MenuItem value="openai">OpenAI</MenuItem>
            <MenuItem value="google">Google AI</MenuItem>
          </Select>
          <TextField size="small" label="From" type="datetime-local" value={filterFrom} onChange={e => onFilterFromChange(e.target.value)} InputLabelProps={{ shrink: true }} />
          <TextField size="small" label="To" type="datetime-local" value={filterTo} onChange={e => onFilterToChange(e.target.value)} InputLabelProps={{ shrink: true }} />
          <Button variant="outlined" onClick={onRefresh} size="small">Refresh</Button>
        </Box>
      </Box>
      <Divider />
      {isLoading ? (
        <Typography sx={{ p: 2 }}>Loading...</Typography>
      ) : logs.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No LLM interactions recorded yet.
        </Typography>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Provider</TableCell>
                <TableCell>Model</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Tokens</TableCell>
                <TableCell>Duration</TableCell>
                <TableCell>Origin</TableCell>
                <TableCell>Time</TableCell>
                <TableCell></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {logs.map((log: any) => (
                <TableRow key={log.id} sx={{ '&:nth-of-type(odd)': { bgcolor: 'action.hover' } }}>
                  <TableCell>{getProviderIcon(log.providerType)} {log.providerType}</TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', fontSize: 11 }}>{log.model}</TableCell>
                  <TableCell>
                    <Chip label={log.success ? 'OK' : 'Fail'} size="small" color={getStatusColor(log.success) as any} variant="outlined" />
                  </TableCell>
                  <TableCell sx={{ fontSize: 11 }}>
                    {formatTokens(log.promptTokens)} / {formatTokens(log.completionTokens)}
                  </TableCell>
                  <TableCell>{log.durationMs}ms</TableCell>
                  <TableCell sx={{ fontSize: 11 }}>
                    {log.origin}{log.originAgent ? `:${log.originAgent}` : ''}
                  </TableCell>
                  <TableCell>{new Date(log.startedAt).toLocaleTimeString()}</TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', gap: 0.5 }}>
                      <IconButton size="small" onClick={() => onOpenDetail(log)} color="primary">
                        <HistoryIcon fontSize="small" />
                      </IconButton>
                      <IconButton size="small" onClick={() => onDelete(log.id)} color="error">
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
}
