
// ==================== Markdown Support ====================

/** Message types that render with markdown */
const MARKDOWN_TYPES = new Set<UnifiedMessageType>([
  'inGamePublic', 'inGameWhisper', 'oocPublic', 'oocWhisper',
  'narration', 'gm', 'plotReview', 'consistencyCheck', 'suggestion'
]);

/** Check if content contains markdown syntax */
function hasMarkdownSyntax(content: string): boolean {
  // Check for common markdown patterns
  return /\*\*|\*_|~~|`{1,3}|\[.+\]\(|^#{1,6}\s|^[\-\*\+]\s|^[0-9]+\.\s|^>\s|^---|^- \[|^- \[x\]/m.test(content);
}

// ==================== Message Color Config ====================
