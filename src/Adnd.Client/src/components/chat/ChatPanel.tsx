import { useCallback, useRef } from 'react';
import { Box, Typography, Paper } from '@mui/material';
import MessageBubble from './MessageBubble';
import type { MessagePaginated } from '../../types';
import type { UnifiedMessage } from '../../api/hooks/useMessages';

interface ChatPanelProps {
  messages: MessagePaginated[];
  isLoadingMore: boolean;
  hasMore: boolean;
  loadMoreOldest: () => void;
  onSend?: () => Promise<void>;
  messagesEndRef?: React.RefObject<HTMLDivElement | null>;
  isMobile?: boolean;
  newMessagesCount?: number;
  onScrollToBottom?: () => void;
}

export default function ChatPanel({
  messages, isLoadingMore, hasMore, loadMoreOldest,
  onSend, messagesEndRef, isMobile = false,
  newMessagesCount = 0, onScrollToBottom
}: ChatPanelProps) {
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  // Convert MessagePaginated to UnifiedMessage for MessageBubble
  const toUnifiedMessage = (msg: MessagePaginated): UnifiedMessage => ({
    id: msg.id,
    type: msg.isOOC ? 'oocPublic' : 'inGamePublic',
    content: msg.content,
    senderName: msg.playerName,
    senderRole: '',
    timestamp: msg.createdAt,
    isSystem: msg.type === 0,
    isWhisper: false,
    diceFormula: msg.metadata?.diceFormula,
    diceTotal: msg.metadata?.diceTotal,
    skill: msg.metadata?.skill,
    skillDC: msg.metadata?.skillDC,
  });

  // Scroll detection: load older messages when scrolling to the top
  const handleScroll = useCallback(() => {
    const el = scrollContainerRef.current;
    if (!el) return;
    if (el.scrollTop < 100 && hasMore && !isLoadingMore) {
      loadMoreOldest();
    }
  }, [hasMore, isLoadingMore, loadMoreOldest]);

  return (
    <Paper sx={{
      height: isMobile ? 'calc(100dvh - 220px)' : '75vh',
      minHeight: isMobile ? 300 : 400,
      display: 'flex',
      flexDirection: 'column',
    }}>
      {/* ===== Message Feed ===== */}
      <Box sx={{ flex: 1, overflow: 'auto', p: isMobile ? 1 : 1.5 }} ref={scrollContainerRef} onScroll={handleScroll}>
        {/* Loading indicator for older messages */}
        {isLoadingMore && (
          <Typography variant="caption" color="text.secondary" sx={{ textAlign: 'center', py: 1 }}>
            Loading older messages...
          </Typography>
        )}

        {/* New messages indicator */}
        {newMessagesCount > 0 && onScrollToBottom && (
          <Box sx={{ textAlign: 'center', py: 0.5 }}>
            <Box sx={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 0.5,
              bgcolor: 'primary.main',
              color: 'primary.contrastText',
              borderRadius: 2,
              px: 1.5,
              py: 0.5,
              cursor: 'pointer',
              fontSize: 12,
              fontWeight: 600,
              '&:hover': { bgcolor: 'primary.dark' }
            }} onClick={onScrollToBottom}>
              ▼ {newMessagesCount} new message{newMessagesCount > 1 ? 's' : ''}
            </Box>
          </Box>
        )}

        {messages.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 8 }}>
            No messages yet. Start the conversation!
          </Typography>
        ) : (
          messages.map((msg) => (
            <MessageBubble key={msg.id} msg={toUnifiedMessage(msg)} />
          ))
        )}
        <div ref={messagesEndRef} />
      </Box>

      {/* ===== Input Area ===== */}
      <Box sx={{
        p: isMobile ? 1 : 1.5,
        borderTop: 1,
        borderColor: 'divider',
        bgcolor: 'background.paper'
      }}>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <input
            type="text"
            placeholder="Type a message..."
            style={{
              flex: 1,
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 2,
              padding: '8px 12px',
              fontSize: 14,
              outline: 'none',
              bgcolor: 'background.default',
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                onSend?.();
              }
            }}
          />
          <button
            onClick={() => onSend?.()}
            disabled={false}
            style={{
              padding: '8px 16px',
              border: 'none',
              borderRadius: 2,
              bgcolor: 'primary.main',
              color: 'primary.contrastText',
              cursor: 'pointer',
              fontWeight: 600,
              fontSize: 14,
            }}
          >
            Send
          </button>
        </Box>
      </Box>
    </Paper>
  );
}
