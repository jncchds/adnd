import { Box, Typography, Paper, Divider } from '@mui/material'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

// Baked in by vite.config.ts from the repo's RELEASE_NOTES.md, so the page cannot drift from
// the file the maintainer actually edits.
const NOTES = __RELEASE_NOTES__

// The file's own "# Release Notes" heading is dropped — the page already has a title, and two
// of them stacked reads like a mistake.
const BODY = NOTES.replace(/^#\s+Release Notes\s*\n/, '').trim()

export default function ReleaseNotesPage() {
  return (
    <Box sx={{ px: { xs: 2, md: 6 }, py: { xs: 4, md: 6 }, width: '100%' }}>
      <Typography variant="h4" fontWeight={800} color="primary" gutterBottom>
        Release Notes
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Currently on v{__APP_VERSION__}.
      </Typography>
      <Divider sx={{ mb: 3 }} />

      <Paper
        variant="outlined"
        sx={{
          p: { xs: 2, md: 4 },
          '& h2': {
            fontSize: '1.5rem', fontWeight: 700, color: 'primary.main',
            mt: 5, mb: 2, pb: 1, borderBottom: '1px solid', borderColor: 'divider',
          },
          '& h2:first-of-type': { mt: 0 },
          '& h3': { fontSize: '1.1rem', fontWeight: 700, mt: 3, mb: 1 },
          '& p': { my: 1.5, lineHeight: 1.7 },
          '& ul, & ol': { pl: 3, my: 1.5 },
          '& li': { mb: 0.75, lineHeight: 1.7 },
          '& code': {
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
            fontSize: '0.85em', px: 0.5, py: 0.2, borderRadius: 0.5,
            bgcolor: 'action.hover',
          },
          '& pre': { p: 2, borderRadius: 1, bgcolor: 'action.hover', overflowX: 'auto' },
          '& pre code': { bgcolor: 'transparent', p: 0 },
          '& a': { color: 'primary.main' },
          '& table': { borderCollapse: 'collapse', width: '100%', my: 2, display: 'block', overflowX: 'auto' },
          '& th, & td': { border: '1px solid', borderColor: 'divider', px: 1.5, py: 0.75, textAlign: 'left' },
          '& th': { bgcolor: 'action.hover', fontWeight: 700 },
          '& blockquote': {
            m: 0, my: 2, pl: 2, borderLeft: '3px solid', borderColor: 'primary.main',
            color: 'text.secondary',
          },
        }}
      >
        {BODY
          ? <ReactMarkdown remarkPlugins={[remarkGfm]}>{BODY}</ReactMarkdown>
          : <Typography variant="body2" color="text.secondary">No release notes are available for this build.</Typography>}
      </Paper>
    </Box>
  )
}
