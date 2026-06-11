import type { NPCUpdateRequest, PlotThreadUpdateRequest, CharacterUpdateRequest } from '../../types/game.types';
import { api } from '../client';

// NPCs
export async function adminGetNPCs(gameId: string) {
  return api.getNPCs(gameId);
}

export async function adminCreateNPC(gameId: string, name: string, description?: string) {
  return api.createNPC(gameId, name, description);
}

export async function adminUpdateNPC(npcId: string, updates: Partial<NPCUpdateRequest>) {
  return api.updateNPC(npcId, updates);
}

export async function adminDeleteNPC(npcId: string) {
  return api.deleteNPC(npcId);
}

// Plots
export async function adminGetPlotThreads(gameId: string) {
  return api.getPlotThreads(gameId);
}

export async function adminCreatePlotThread(gameId: string, title: string, description: string) {
  return api.createPlotThread(gameId, title, description);
}

export async function adminUpdatePlotThread(threadId: string, updates: Partial<PlotThreadUpdateRequest>) {
  return api.updatePlotThread(threadId, updates);
}

// PlotWeaver
export async function adminGetPlotWeaverThreads(gameId: string) {
  return api.getPlotWeaverThreads(gameId);
}

export async function adminTriggerPlotReview(gameId: string, context?: string) {
  return api.triggerPlotReview(gameId, context);
}

export async function adminGetPlotReviewHistory(gameId: string, limit = 20) {
  return api.getPlotReviewHistory(gameId, limit);
}

export async function adminAdjustThreadMomentum(gameId: string, threadId: string, delta: number, reason: string) {
  return api.adjustThreadMomentum(gameId, threadId, delta, reason);
}

export async function adminDetectOpportunities(gameId: string) {
  return api.detectOpportunities(gameId);
}

// Characters
export async function adminGetCharacters(gameId: string) {
  return api.getCharacters(gameId);
}

export async function adminGetCharacter(characterId: string) {
  return api.getCharacter(characterId);
}

export async function adminUpdateCharacter(characterId: string, updates: Partial<CharacterUpdateRequest>) {
  return api.updateCharacter(characterId, updates);
}

// Game State
export async function adminGetGameState(gameId: string) {
  return api.getGameState(gameId);
}

export async function adminUpdateGameState(gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) {
  return api.updateGameState(gameId, gameState, plotSeed, gameParameters);
}

// RAG
export async function adminGetPlotContext(gameId: string, maxMessages = 20) {
  return api.getPlotContext(gameId, maxMessages);
}

export async function adminFindSimilarThreads(gameId: string, query: string, limit = 5) {
  return api.findSimilarThreads(gameId, query, limit);
}

export async function adminCheckConsistency(gameId: string, messageCount = 50) {
  return api.checkConsistency(gameId, messageCount);
}

export async function adminGetSessionSummary(gameId: string, sessionId: string, messageCount = 30) {
  return api.getSessionSummary(gameId, sessionId, messageCount);
}

// Systems
export async function adminGetSystems() {
  return api.getSystems();
}
