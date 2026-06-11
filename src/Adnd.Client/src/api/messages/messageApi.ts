import { api } from '../client';

export async function messagesGetPaginated(gameId: string, sessionId: string, page = 1, pageSize = 50, type?: number, anchorId?: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (type !== undefined) params.set('type', String(type));
  if (anchorId) params.set('anchorId', anchorId);
  return api.getMessagesPaginated(gameId, sessionId, page, pageSize, type, anchorId);
}

export async function messagesSearch(gameId: string, sessionId: string, query: string, queryEmbedding: number[], limit = 10) {
  return api.searchMessages(gameId, sessionId, query, queryEmbedding, limit);
}
