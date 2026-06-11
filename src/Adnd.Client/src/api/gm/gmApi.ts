import type { ExecuteToolRequest, SpellUpdateRequest } from '../../types/gm.types';
import { api } from '../client';

// GM Tools
export async function gmGetTools(gameId: string) {
  return api.getGMTools(gameId);
}

export async function gmGetToolsByCategory(gameId: string, category: string) {
  return api.getGMToolsByCategory(gameId, category);
}

export async function gmExecuteTool(gameId: string, sessionId: string, request: ExecuteToolRequest) {
  return api.executeGMTool(gameId, sessionId, request);
}

// Spells
export async function gmGetSpells(characterId: string) {
  return api.getCharacterSpells(characterId);
}

export async function gmUpdateSpells(characterId: string, spells: SpellUpdateRequest[]) {
  return api.updateCharacterSpells(characterId, spells);
}

export async function gmUpdateSpell(characterId: string, spellName: string, spell: SpellUpdateRequest) {
  return api.updateSingleSpell(characterId, spellName, spell);
}

export async function gmRemoveSpell(characterId: string, spellName: string) {
  return api.removeSpell(characterId, spellName);
}

// Tool Calls
export async function gmGetPendingToolCalls(gameId: string) {
  return api.getPendingToolCalls(gameId);
}

export async function gmConfirmToolCall(gameId: string, toolCallId: string, approved: boolean) {
  return api.confirmToolCall(gameId, toolCallId, approved);
}

export async function gmConfirmPlayerRoll(gameId: string, toolCallId: string) {
  return api.confirmPlayerRoll(gameId, toolCallId);
}

export async function gmDeclinePlayerRoll(gameId: string, toolCallId: string) {
  return api.declinePlayerRoll(gameId, toolCallId);
}

// Dice History
export async function gmGetDiceHistory(gameId: string, params?: { sessionId?: string; playerId?: string; limit?: number; sortBy?: string }) {
  const searchParams = new URLSearchParams();
  if (params?.sessionId) searchParams.set('sessionId', params.sessionId);
  if (params?.playerId) searchParams.set('playerId', params.playerId);
  if (params?.limit) searchParams.set('limit', String(params.limit));
  if (params?.sortBy) searchParams.set('sortBy', params.sortBy);
  return api.getDiceHistory(gameId, { sessionId: params?.sessionId, playerId: params?.playerId, limit: params?.limit, sortBy: params?.sortBy });
}

export async function gmGetDiceRoll(gameId: string, messageId: string) {
  return api.getDiceRoll(gameId, messageId);
}

// Session Notes
export async function gmGetSessionNotes(gameId: string, sessionId: string) {
  return api.getSessionNotes(gameId, sessionId);
}

export async function gmCreateSessionNote(gameId: string, sessionId: string, title: string, content: string) {
  return api.createSessionNote(gameId, sessionId, title, content);
}

export async function gmUpdateSessionNote(gameId: string, noteId: string, title: string, content: string) {
  return api.updateSessionNote(gameId, noteId, title, content);
}

export async function gmDeleteSessionNote(gameId: string, noteId: string) {
  return api.deleteSessionNote(gameId, noteId);
}

// Dice Stats
export async function gmGetDiceStats(gameId: string, sessionId?: string) {
  const params = new URLSearchParams();
  if (sessionId) params.set('sessionId', sessionId);
  return api.getDiceStats(gameId, sessionId);
}

export async function gmGetPlayerDiceStats(gameId: string, playerId: string, sessionId?: string) {
  const params = new URLSearchParams();
  if (sessionId) params.set('sessionId', sessionId);
  return api.getPlayerDiceStats(gameId, playerId, sessionId);
}
