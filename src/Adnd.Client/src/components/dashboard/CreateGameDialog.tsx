import { useState } from 'react';
import { Box, Typography, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Button, Chip, Autocomplete, CircularProgress } from '@mui/material';
import type { LLMPreset } from '../../types/llm.types';

interface CreateGameDialogProps {
  open: boolean;
  onClose: () => void;
  onCreate: (name: string, systemId: string, llmPresetId: string | null, plotSeed: string, gameParameters: string, language: string) => Promise<void>;
  presets: LLMPreset[] | undefined;
  presetsLoading: boolean;
  templates: any[];
  selectedTemplateId: string | null;
  onSelectedTemplateChange: (id: string | null) => void;
  onDeleteTemplate: (id: string) => void;
}

export default function CreateGameDialog({
  open, onClose, onCreate, presets, presetsLoading,
  templates, selectedTemplateId, onSelectedTemplateChange, onDeleteTemplate,
}: CreateGameDialogProps) {
  const [gameName, setGameName] = useState('');
  const [systemId, setSystemId] = useState('dnd5e');
  const [llmPresetId, setLLMPresetId] = useState<string | null>(null);
  const [plotSeed, setPlotSeed] = useState('');
  const [gameParameters, setGameParameters] = useState('');
  const [language, setLanguage] = useState('English');
  const [showTemplateDialog, setShowTemplateDialog] = useState(false);
  const [templateName, setTemplateName] = useState('');
  const [templateDefaultName, setTemplateDefaultName] = useState('');

  const handleClose = () => {
    onClose();
    setGameName('');
    setLLMPresetId(null);
    setPlotSeed('');
    setGameParameters('');
    setLanguage('English');
  };

  const handleCreate = async () => {
    if (!gameName.trim()) return;
    await onCreate(gameName, systemId, llmPresetId, plotSeed, gameParameters, language);
    handleClose();
  };

  const handleSelectTemplate = (t: any) => {
    onSelectedTemplateChange(t.id);
    setSystemId(t.systemId);
    setLLMPresetId(t.llmPresetId || null);
    setLanguage(t.language);
    setPlotSeed(t.plotSeed || '');
    setGameParameters(t.gameParameters || '');
    setGameName(t.defaultName || '');
  };

  return (
    <>
      <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
        <DialogTitle>Create New Game</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
            <Typography variant="subtitle2" color="text.secondary">Load from template:</Typography>
            <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', flex: 1 }}>
              {templates.length === 0 ? (
                <Chip label="No templates yet" size="small" variant="outlined" color="default" />
              ) : (
                templates.map(t => (
                  <Chip
                    key={t.id}
                    label={t.name}
                    size="small"
                    clickable
                    onClick={() => handleSelectTemplate(t)}
                    sx={{
                      bgcolor: selectedTemplateId === t.id ? 'primary.lighter' : 'background.default',
                      borderColor: selectedTemplateId === t.id ? 'primary.main' : 'divider',
                      borderWidth: 1,
                      borderStyle: 'solid',
                    }}
                    onDelete={() => {
                      onDeleteTemplate(t.id);
                      if (selectedTemplateId === t.id) onSelectedTemplateChange(null);
                    }}
                  />
                ))
              )}
              <Chip label="+ New Template" size="small" clickable onClick={() => { setShowTemplateDialog(true); setTemplateName(''); setTemplateDefaultName(''); }} />
            </Box>
          </Box>
          <TextField fullWidth label="Game Name" value={gameName} onChange={e => setGameName(e.target.value)} autoFocus />
          <TextField fullWidth select label="System" value={systemId} onChange={e => setSystemId(e.target.value)} SelectProps={{ native: true }}>
            <option value="dnd5e">D&D 5th Edition</option>
            <option value="pf2e">Pathfinder 2nd Edition</option>
            <option value="coc7e">Call of Cthulhu 7th Edition</option>
          </TextField>
          <Box sx={{ mb: 2 }}>
            <TextField fullWidth select label="Narration Language" value={language} onChange={e => setLanguage(e.target.value)} SelectProps={{ native: true }}>
              <option value="English">English</option>
              <option value="Spanish">Español (Spanish)</option>
              <option value="French">Français (French)</option>
              <option value="German">Deutsch (German)</option>
              <option value="Italian">Italiano (Italian)</option>
              <option value="Portuguese">Português (Portuguese)</option>
              <option value="Japanese">日本語 (Japanese)</option>
              <option value="Korean">한국어 (Korean)</option>
              <option value="Chinese">中文 (Chinese)</option>
              <option value="Ukrainian">Українська (Ukrainian)</option>
              <option value="Polish">Polski (Polish)</option>
              <option value="Dutch">Nederlands (Dutch)</option>
              <option value="Swedish">Svenska (Swedish)</option>
              <option value="Norwegian">Norsk (Norwegian)</option>
              <option value="Finnish">Suomi (Finnish)</option>
              <option value="Danish">Dansk (Danish)</option>
              <option value="Greek">Ελληνικά (Greek)</option>
              <option value="Turkish">Türkçe (Turkish)</option>
              <option value="Arabic">العربية (Arabic)</option>
              <option value="Hindi">हिन्दी (Hindi)</option>
            </TextField>
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mt: 1 }}>
              <Typography variant="caption" color="text.secondary">Or type a custom language:</Typography>
            </Box>
            <TextField fullWidth size="small" placeholder="e.g., Esperanto, Klingon, etc." value={language} onChange={e => setLanguage(e.target.value)} sx={{ mt: 0.5 }} />
          </Box>
          <Autocomplete
            options={presets || []}
            value={presets?.find(p => p.id === llmPresetId) || null}
            onChange={(_event, newValue) => setLLMPresetId(newValue?.id || null)}
            getOptionLabel={(option) => option.name}
            isOptionEqualToValue={(option, value) => option.id === value.id}
            disabled={presetsLoading || presets?.length === 0}
            renderInput={(params) => (
              <TextField {...params} fullWidth label="LLM Preset (for AI-GM)" placeholder={presetsLoading ? 'Loading presets...' : presets?.length === 0 ? 'No presets available' : 'Select an LLM preset'} InputProps={{ ...params.InputProps, endAdornment: presetsLoading ? <CircularProgress color="inherit" size={20} /> : params.InputProps?.endAdornment }} />
            )}
            renderOption={(props, option) => (
              <Box component="li" {...props} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Typography variant="body2">{option.name}</Typography>
                <Chip label={option.providerType} size="small" variant="outlined" sx={{ height: 16, fontSize: 10 }} />
              </Box>
            )}
          />
          <TextField fullWidth multiline rows={3} label="Plot Seed (initial story premise)" value={plotSeed} onChange={e => setPlotSeed(e.target.value)} placeholder="Describe the initial story, setting, and tone..." />
          <TextField fullWidth multiline rows={2} label="Game Parameters (tone, difficulty, pacing)" value={gameParameters} onChange={e => setGameParameters(e.target.value)} placeholder="e.g., Dark tone, medium difficulty, fast-paced..." />
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancel</Button>
          <Button onClick={() => { setShowTemplateDialog(true); setTemplateName(''); setTemplateDefaultName(''); }} variant="outlined" disabled={!gameName.trim()}>💾 Save as Template</Button>
          <Button onClick={handleCreate} variant="contained" disabled={!gameName.trim()}>Create</Button>
        </DialogActions>
      </Dialog>

      <SaveTemplateDialog
        open={showTemplateDialog}
        onClose={() => setShowTemplateDialog(false)}
        templateName={templateName}
        setTemplateName={setTemplateName}
        templateDefaultName={templateDefaultName}
        setTemplateDefaultName={setTemplateDefaultName}
        systemId={systemId}
        llmPresetId={llmPresetId}
        presets={presets}
        language={language}
        plotSeed={plotSeed}
        gameParameters={gameParameters}
        onSave={async (_name, _defaultName) => {
          // Will be passed down from parent
        }}
      />
    </>
  );
}

interface SaveTemplateDialogProps {
  open: boolean;
  onClose: () => void;
  templateName: string;
  setTemplateName: (v: string) => void;
  templateDefaultName: string;
  setTemplateDefaultName: (v: string) => void;
  systemId: string;
  llmPresetId: string | null;
  presets: LLMPreset[] | undefined;
  language: string;
  plotSeed: string;
  gameParameters: string;
  onSave: (_name: string, _defaultName: string) => Promise<void>;
}

function SaveTemplateDialog({ open, onClose, templateName, setTemplateName, templateDefaultName, setTemplateDefaultName, systemId, llmPresetId, presets, language, plotSeed, gameParameters, onSave }: SaveTemplateDialogProps) {
  const handleSave = async () => {
    if (!templateName.trim()) return;
    await onSave(templateName, templateDefaultName);
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Save as Template</DialogTitle>
      <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Typography variant="body2" color="text.secondary">Save your current game configuration as a reusable template.</Typography>
        <TextField fullWidth label="Template Name" value={templateName} onChange={e => setTemplateName(e.target.value)} placeholder="e.g., D&D Fantasy Adventure" helperText="A short name to identify this template" />
        <TextField fullWidth label="Default Game Name" value={templateDefaultName} onChange={e => setTemplateDefaultName(e.target.value)} placeholder="e.g., My New Adventure" helperText="Pre-filled game name (optional)" />
        <Box sx={{ bgcolor: 'background.default', p: 1.5, borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Will save:</Typography>
          <Typography variant="caption" sx={{ display: 'block' }}>🎮 System: {systemId}</Typography>
          {llmPresetId && <Typography variant="caption" sx={{ display: 'block' }}>🤖 LLM Preset: {presets?.find(p => p.id === llmPresetId)?.name}</Typography>}
          <Typography variant="caption" sx={{ display: 'block' }}>🌐 Language: {language}</Typography>
          {plotSeed && <Typography variant="caption" sx={{ display: 'block' }}>📖 Plot Seed: {plotSeed.substring(0, 60)}{plotSeed.length > 60 ? '...' : ''}</Typography>}
          {gameParameters && <Typography variant="caption" sx={{ display: 'block' }}>⚙️ Parameters: {gameParameters.substring(0, 60)}{gameParameters.length > 60 ? '...' : ''}</Typography>}
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSave} variant="contained" disabled={!templateName.trim()}>Save Template</Button>
      </DialogActions>
    </Dialog>
  );
}
