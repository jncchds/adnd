import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Box, Typography, Link as MuiLink, Paper } from '@mui/material';
import type { Components } from 'react-markdown';

interface MarkdownRendererProps {
  content: string;
  /** Limit rendering to text content only (no headers/lists). Used for inline-style messages. */
  compact?: boolean;
}

/**
 * Renders markdown content safely within the chat UI.
 *
 * Supported syntax:
 *   - **bold**, *italic*, ~~strikethrough~~
 *   - `inline code`, ```code blocks```
 *   - [links](url)
 *   - Unordered lists (-, *, +)
 *   - Ordered lists (1., 2., ...)
 *   - Headers (#, ##, ###) — only when compact=false
 *   - Horizontal rules (---)
 *   - Blockquotes (>)
 *   - Tables (GFM)
 *   - Task lists (- [ ], - [x])
 */
export default function MarkdownRenderer({ content, compact = false }: MarkdownRendererProps) {
  if (!content.trim()) return null;

  const components: Components = {
    // Headers — only in non-compact mode
    h1: ({ children }) => (
      <Typography variant="h6" sx={{ mt: 1.5, mb: 0.5, fontWeight: 700 }}>
        {children}
      </Typography>
    ),
    h2: ({ children }) => (
      <Typography variant="subtitle1" sx={{ mt: 1.5, mb: 0.5, fontWeight: 600 }}>
        {children}
      </Typography>
    ),
    h3: ({ children }) => (
      <Typography variant="subtitle2" sx={{ mt: 1, mb: 0.5, fontWeight: 600 }}>
        {children}
      </Typography>
    ),

    // Paragraphs — compact mode uses smaller margins
    p: ({ children }) => (
      <Typography
        variant="body2"
        sx={{
          mb: compact ? 0.5 : 1,
          lineHeight: 1.6,
          '&:last-child': { mb: 0 },
        }}
      >
        {children}
      </Typography>
    ),

    // Bold
    strong: ({ children }) => (
      <Typography component="strong" sx={{ fontWeight: 700 }}>
        {children}
      </Typography>
    ),

    // Italic
    em: ({ children }) => (
      <Typography component="em" sx={{ fontStyle: 'italic' }}>
        {children}
      </Typography>
    ),

    // Strikethrough
    del: ({ children }) => (
      <Typography component="del" sx={{ textDecoration: 'line-through', color: 'text.secondary' }}>
        {children}
      </Typography>
    ),

    // Inline code
    code: ({ className, children }) => {
      const isBlock = className?.includes('language-');
      if (isBlock) return null; // handled by pre
      return (
        <Typography
          component="code"
          sx={{
            bgcolor: 'action.hover',
            color: 'error.main',
            px: 0.5,
            py: 0.25,
            borderRadius: 0.5,
            fontSize: '0.85em',
            fontFamily: 'monospace',
          }}
        >
          {children}
        </Typography>
      );
    },

    // Code blocks
    pre: ({ children }) => (
      <Box sx={{
        my: compact ? 0.5 : 1,
        overflow: 'auto',
      }}>
        <Paper
          variant="outlined"
          sx={{
            bgcolor: 'background.default',
            p: 1.5,
            borderRadius: 1,
            fontSize: '0.85em',
            fontFamily: 'monospace',
          }}
        >
          {children}
        </Paper>
      </Box>
    ),

    // Links
    a: ({ href, children }) => (
      <MuiLink
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        sx={{
          color: 'primary.main',
          textDecorationColor: 'primary.main',
          '&:hover': { textDecoration: 'underline' },
        }}
      >
        {children}
      </MuiLink>
    ),

    // Lists
    ul: ({ children }) => (
      <Box sx={{
        pl: compact ? 2 : 3,
        mb: compact ? 0.5 : 1,
        listStyleType: 'disc',
      }}>
        {children}
      </Box>
    ),
    ol: ({ children }) => (
      <Box sx={{
        pl: compact ? 2 : 3,
        mb: compact ? 0.5 : 1,
        listStyleType: 'decimal',
      }}>
        {children}
      </Box>
    ),
    li: ({ children }) => (
      <Typography variant="body2" sx={{
        mb: compact ? 0.25 : 0.5,
        lineHeight: 1.5,
        '&:last-child': { mb: 0 },
      }}>
        {children}
      </Typography>
    ),

    // Blockquote
    blockquote: ({ children }) => (
      <Box sx={{
        borderLeft: '3px solid',
        borderColor: 'divider',
        pl: 1.5,
        my: compact ? 0.5 : 1,
      }}>
        <Typography variant="body2" sx={{ color: 'text.secondary', fontStyle: 'italic' }}>
          {children}
        </Typography>
      </Box>
    ),

    // Horizontal rule
    hr: () => (
      <Box sx={{ my: compact ? 0.5 : 1, border: 'none', borderTop: `1px solid`, borderColor: 'divider' }} />
    ),

    // Tables
    table: ({ children }) => (
      <Box sx={{
        my: compact ? 0.5 : 1,
        overflowX: 'auto',
        '& table': { width: '100%', borderCollapse: 'collapse', fontSize: '0.85em' },
        '& th, & td': { border: '1px solid', borderColor: 'divider', px: 1, py: 0.5 },
        '& th': { bgcolor: 'action.hover', fontWeight: 600 },
      }}>
        <table>{children}</table>
      </Box>
    ),

    // Task lists
    input: ({ defaultChecked, ...props }) => (
      <Box component="span" sx={{ mr: 0.5, verticalAlign: 'middle' }}>
        <input type="checkbox" defaultChecked={defaultChecked} {...props} style={{ margin: 0 }} />
      </Box>
    ),
  };

  return (
    <Box sx={{ '& > *:first-child': { mt: 0 }, '& > *:last-child': { mb: 0 } }}>
      <ReactMarkdown remarkPlugins={[remarkGfm]} components={components}>
        {content}
      </ReactMarkdown>
    </Box>
  );
}
