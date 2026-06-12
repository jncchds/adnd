import { useCallback, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../api/hooks/useAuth';
import { useGame } from '../api/hooks/useGameDetail';
import { useToolCalls } from '../api/hooks/useAgent';
import { useMessagesInfiniteScroll } from '../api/hooks/useMessages';
import { api } from '../api/client';
import ToolCallBanner from '../components/ToolCallBanner';
import ChatPanel from '../components/chat/ChatPanel';
import { Box, Typography, TextField, InputAdornment, MenuItem, Select, FormControl, InputLabel, IconButton } from '@mui/material';
import { Send as SendIcon, SportsEsports as DiceIcon } from '@mui/icons-material';

type MessageInputType = 'inGame' | 'ooc';

export default function GameChatPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { game, isLoading: isLoadingGame } = useGame(id);
  const { pendingCalls: calls } = useToolCalls(id);
  const { messages, isLoading, hasMore, loadOldest } = useMessagesInfiniteScroll(id, undefined);
  const [messageInput, setMessageInput] = useState('');
  const [messageType, setMessageType] = useState<MessageInputType>('inGame');
  const chatRef = useRef<HTMLDivElement>(null);

  const sendMessage = useCallback(async () => {
    if (!messageInput.trim() || !id) return;
    // TODO: implement send via hub
    setMessageInput('');
  }, [messageInput, id]);

  const handleRollDice = useCallback(async () => {
    // TODO: implement dice roll
  }, []);

  const handleLeaveGame = useCallback(async () => {
    if (!id) return;
    await api.leaveGame(id);
    navigate('/');
  }, [id, navigate]);

  void handleLeaveGame;

  if (isLoadingGame) return <Typography>Loading...</Typography>;
  if (!game) return <Typography>No game data available.</Typography>;

  return (
    <Box sx={{ display: 'flex', height: '100vh' }}>
      <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        <Box sx={{ flex: 1, overflow: 'auto', p: 2 }} ref={chatRef}>
          <ChatPanel
            messages={messages}
            isLoadingMore={isLoading}
            hasMore={hasMore}
            loadMoreOldest={loadOldest}
          />
        </Box>

        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', p: 2, pt: 0 }}>
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <InputLabel>Type</InputLabel>
            <Select
              value={messageType}
              label="Type"
              onChange={(e) => setMessageType(e.target.value as MessageInputType)}
            >
              <MenuItem value="inGame">In-Game</MenuItem>
              <MenuItem value="ooc">OOC</MenuItem>
            </Select>
          </FormControl>

          <TextField
            fullWidth
            placeholder={`Send ${messageType} message...`}
            value={messageInput}
            onChange={(e) => setMessageInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && sendMessage()}
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={sendMessage} edge="end">
                    <SendIcon />
                  </IconButton>
                  <IconButton onClick={handleRollDice} edge="end">
                    <DiceIcon />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
        </Box>
      </Box>

      <ToolCallBanner
        pendingCalls={calls || []}
        onConfirm={async (_callId, _approved) => {}}
        onRoll={async (_callId) => {}}
        onDecline={async (_callId) => {}}
        onDismiss={(_callId) => {}}
        isCreator={user?.role === 'Creator'}
      />
    </Box>
  );
}
