import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useGMTools } from '../api/hooks/useGM';
import {
  Box, Typography, Paper, Chip, List, ListItem, ListItemText,
  ListItemAvatar, Avatar, Button, TextField, Alert, AlertTitle,
  IconButton, Collapse,
} from '@mui/material';
import {
  Build as ToolIcon, Replay as RefreshIcon,
  ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon,
  Cancel as CancelIcon,
  PlayArrow as ExecuteIcon,
  FilterList as FilterIcon,
  Code as CodeIcon,
} from '@mui/icons-material';

export default function GMToolPanel() {
  const { id: gameId } = useParams<{ id: string }>();
  const { tools, isLoading, refetch, executeTool } = useGMTools(gameId);
  const [selectedTool, setSelectedTool] = useState<any>(null);
  const [toolArgs, setToolArgs] = useState('');
  const [showFilter, setShowFilter] = useState(false);
  const [filterCategory, setFilterCategory] = useState('');
  const [showArgs, setShowArgs] = useState(false);
  const [result, setResult] = useState<{ success: boolean; message: string } | null>(null);
  const [showResult, setShowResult] = useState(false);

  useEffect(() => {
    refetch();
  }, [gameId]);

  const handleExecute = async () => {
    if (!selectedTool || !gameId) return;
    setResult(null);
    setShowResult(false);
    try {
      const args = JSON.parse(toolArgs || '{}');
      const response = await executeTool(selectedTool.name, args);
      if (response) {
        setResult({
          success: response.success,
          message: response.outputMessage || response.output || 'Tool executed successfully',
        });
      } else {
        setResult({ success: false, message: 'Failed to execute tool' });
      }
    } catch (e: any) {
      setResult({ success: false, message: e.message || 'Failed to parse arguments' });
    }
    setShowResult(true);
  };

  const categoryIcons: Record<string, string> = {
    'Auto': '🤖', 'PlayerRoll': '🎲', 'SystemRoll': '🎯',
    'Combat': '⚔️', 'Narrative': '📖', 'Query': '🔍',
    'StateManagement': '⚙️',
  };

  const categoryColors: Record<string, 'primary' | 'secondary' | 'success' | 'warning' | 'error' | 'info' | 'default'> = {
    'Auto': 'info', 'PlayerRoll': 'warning', 'SystemRoll': 'success',
    'Combat': 'error', 'Narrative': 'primary', 'Query': 'info',
    'StateManagement': 'secondary',
  };

  const filteredTools = filterCategory
    ? tools.filter(t => String(t.category) === filterCategory)
    : tools;

  const categories = [...new Set(tools.map(t => String(t.category)))];

  return (
    <Paper sx={{ height: '75vh', display: 'flex', flexDirection: 'column' }}>
      {/* Header */}
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: 1, borderColor: 'divider' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <ToolIcon color="primary" />
          <Typography variant="h6">GM Tools ({filteredTools.length})</Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button size="small" variant="outlined" startIcon={<FilterIcon />} onClick={() => setShowFilter(!showFilter)}>
            Filter
          </Button>
          <Button size="small" variant="outlined" startIcon={<RefreshIcon />} onClick={refetch}>
            Refresh
          </Button>
        </Box>
      </Box>

      {/* Filter Panel */}
      <Collapse in={showFilter}>
        <Box sx={{ p: 1.5, display: 'flex', gap: 0.5, flexWrap: 'wrap', borderBottom: 1, borderColor: 'divider', bgcolor: 'background.default' }}>
          <Chip label="All" size="small" clickable={!filterCategory} onClick={() => setFilterCategory('')} color={!filterCategory ? 'primary' : 'default'} variant={!filterCategory ? 'filled' : 'outlined'} />
          {categories.map(cat => (
            <Chip
              key={cat}
              label={cat}
              size="small"
              clickable
              onClick={() => setFilterCategory(filterCategory === cat ? '' : cat)}
              color={categoryColors[cat] || 'default'}
              variant={filterCategory === cat ? 'filled' : 'outlined'}
            />
          ))}
        </Box>
      </Collapse>

      {/* Tool List */}
      <Box sx={{ flex: 1, overflow: 'auto', p: 1 }}>
        {isLoading ? (
          <Box sx={{ p: 4, textAlign: 'center' }}>
            <Typography color="text.secondary">Loading tools...</Typography>
          </Box>
        ) : filteredTools.length === 0 ? (
          <Box sx={{ p: 4, textAlign: 'center' }}>
            <Typography variant="body2" color="text.secondary">No tools available for this game.</Typography>
          </Box>
        ) : (
          <List>
            {filteredTools.map(tool => (
              <ListItem
                key={tool.name}
                sx={{
                  mb: 0.5,
                  borderRadius: 1,
                  cursor: 'pointer',
                  bgcolor: selectedTool?.name === tool.name ? 'action.selected' : 'inherit',
                  '&:hover': { bgcolor: 'action.hover' },
                }}
                onClick={() => { setSelectedTool(tool); setToolArgs('{}'); setShowResult(false); }}
              >
                <ListItemAvatar>
                  <Avatar sx={{ bgcolor: `rgba(${categoryColors[tool.category] === 'error' ? '244,67,54' : categoryColors[tool.category] === 'warning' ? '255,152,0' : categoryColors[tool.category] === 'success' ? '76,175,80' : '33,150,243'}, 0.15)` }}>
                    <ToolIcon sx={{ color: `rgba(${categoryColors[tool.category] === 'error' ? '244,67,54' : categoryColors[tool.category] === 'warning' ? '255,152,0' : categoryColors[tool.category] === 'success' ? '76,175,80' : '33,150,243'}, 0.8)` }} fontSize="small" />
                  </Avatar>
                </ListItemAvatar>
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{tool.name}</Typography>
                      <Chip label={tool.requiresConfirmation ? 'Needs Approval' : 'Auto'} size="small" color={tool.requiresConfirmation ? 'warning' : 'success'} variant="outlined" sx={{ height: 18, fontSize: 9 }} />
                    </Box>
                  }
                  secondary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mt: 0.25 }}>
                      <Chip label={categoryIcons[tool.category] || '🔧'} size="small" sx={{ height: 16, fontSize: 10 }} />
                      <Typography variant="caption" color="text.secondary">{tool.description}</Typography>
                    </Box>
                  }
                />
              </ListItem>
            ))}
          </List>
        )}
      </Box>

      {/* Tool Detail Panel */}
      {selectedTool && (
        <Collapse in={!!selectedTool}>
          <Box sx={{ borderTop: 1, borderColor: 'divider', bgcolor: 'background.default' }}>
            <Box sx={{ p: 2 }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>{selectedTool.name}</Typography>
                <IconButton size="small" onClick={() => setSelectedTool(null)}>
                  <CancelIcon fontSize="small" />
                </IconButton>
              </Box>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{selectedTool.description}</Typography>

              {/* Parameters */}
              <Box sx={{ mb: 1 }}>
                <Button size="small" startIcon={<CodeIcon />} onClick={() => setShowArgs(!showArgs)} sx={{ textTransform: 'none' }}>
                  {showArgs ? 'Hide' : 'Show'} Parameters
                  {showArgs ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                </Button>
                <Collapse in={showArgs}>
                  <Box sx={{ mt: 1, p: 1.5, bgcolor: 'background.paper', borderRadius: 1 }}>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                      Required: {(selectedTool.parameters?.required || []).join(', ') || 'None'}
                    </Typography>
                    <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace', whiteSpace: 'pre-wrap', display: 'block' }}>
                      {JSON.stringify(selectedTool.parameters, null, 2)}
                    </Typography>
                  </Box>
                </Collapse>
              </Box>

              {/* Arguments Input */}
              <TextField
                fullWidth
                size="small"
                label="Arguments (JSON)"
                value={toolArgs}
                onChange={e => setToolArgs(e.target.value)}
                placeholder='{"key": "value"}'
                multiline
                minRows={3}
                sx={{ mb: 1 }}
                InputProps={{
                  sx: { fontFamily: 'monospace', fontSize: 12 },
                }}
              />

              {/* Execute Button */}
              <Button
                variant="contained"
                startIcon={<ExecuteIcon />}
                onClick={handleExecute}
                disabled={!toolArgs.trim()}
                fullWidth
              >
                Execute {selectedTool.name}
              </Button>

              {/* Result */}
              <Collapse in={showResult}>
                <Box sx={{ mt: 1 }}>
                  {result && (
                    <Alert severity={result.success ? 'success' : 'error'} onClose={() => setShowResult(false)}>
                      <AlertTitle>{result.success ? 'Success' : 'Error'}</AlertTitle>
                      {result.message}
                    </Alert>
                  )}
                </Box>
              </Collapse>
            </Box>
          </Box>
        </Collapse>
      )}
    </Paper>
  );
}
