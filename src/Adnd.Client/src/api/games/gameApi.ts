import type { NPCUpdateRequest, PlotThreadUpdateRequest, CharacterUpdateRequest } from '../../types/game.types';
import { api } from '../client';

export async function gamesGetGames() {
  return api.getGames();
}

export async function gamesGetGame(id: string) {
  return api.getGame(id);
}

export async function gamesCreateGame(name: string, systemId = 'dnd5e', systemVersion?: string, customSystemJson?: string, llmPresetId?: string, plotSeed?: string, gameParameters?: string, language = 'English') {
  return api.createGame(name, systemId, systemVersion, customSystemJson, llmPresetId, plotSeed, gameParameters, language);
}

export async function gamesUpdateLanguage(gameId: string, language: string) {
  return api.updateGameLanguage(gameId, language);
}

export async function gamesDeleteGame(id: string) {
  return api.deleteGame(id);
}

export async function gamesGenerateInvite(id: string) {
  return api.generateInvite(id);
}

export async function gamesJoinGame(id: string) {
  return api.joinGame(id);
}

export async function gamesJoinByCode(code: string) {
  return api.joinByCode(code);
}

export async function gamesLeaveGame(id: string) {
  return api.leaveGame(id);
}

export async function gamesGetSessions(gameId: string) {
  return api.getSessions(gameId);
}

export async function gamesCreateSession(gameId: string, title: string, description?: string) {
  return api.createSession(gameId, title, description);
}

export async function gamesCloseSession(gameId: string, sessionId: string) {
  return api.closeSession(gameId, sessionId);
}

export async function gamesGetPlayers(gameId: string) {
  return api.getPlayers(gameId);
}

export async function gamesPromotePlayer(gameId: string, playerId: string, role: string) {
  return api.promotePlayer(gameId, playerId, role);
}

export async function gamesGetActiveCombats(gameId: string) {
  return api.getActiveCombats(gameId);
}

export async function gamesStartGame(gameId: string) {
  return api.startGame(gameId);
}

export async function gamesArchiveGame(gameId: string) {
  return api.archiveGame(gameId);
}

export async function gamesGetGMStatus(gameId: string) {
  return api.getGMStatus(gameId);
}

export async function gamesPauseGM(gameId: string) {
  return api.pauseGM(gameId);
}

export async function gamesResumeGM(gameId: string) {
  return api.resumeGM(gameId);
}

export async function gamesSwayStory(gameId: string, direction: string) {
  return api.swayStory(gameId, direction);
}

export async function gamesPauseGame(gameId: string) {
  return api.pauseGame(gameId);
}

export async function gamesResumeGame(gameId: string) {
  return api.resumeGame(gameId);
}

export async function gamesTriggerCombatStart(gameId: string) {
  return api.triggerCombatStart(gameId);
}

export async function gamesTriggerCombatEnd(gameId: string) {
  return api.triggerCombatEnd(gameId);
}

export async function gamesGetWhispers(gameId: string, limit = 50) {
  return api.getWhispers(gameId, limit);
}

export async function gamesSendWhisper(gameId: string, sessionId: string, targets: string, type: number, content: string) {
  return api.sendWhisper(gameId, sessionId, targets, type, content);
}

export async function gamesSendGMWhisper(gameId: string, sessionId: string, targetPlayerIds: string[], type: number, content: string) {
  return api.sendGMWhisper(gameId, sessionId, targetPlayerIds, type, content);
}

export async function gamesCheckConsistency(gameId: string, messageCount = 50) {
  return api.checkConsistency(gameId, messageCount);
}

export async function gamesGetCharacter(characterId: string) {
  return api.getCharacter(characterId);
}

export async function gamesUpdateCharacter(characterId: string, updates: Partial<CharacterUpdateRequest>) {
  return api.updateCharacter(characterId, updates);
}

export async function gamesUpdateGameState(gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) {
  return api.updateGameState(gameId, gameState, plotSeed, gameParameters);
}

export async function gamesDeleteNPC(npcId: string) {
  return api.deleteNPC(npcId);
}

export async function gamesGetPlotThreads(gameId: string) {
  return api.getPlotThreads(gameId);
}

export async function gamesCreatePlotThread(gameId: string, title: string, description: string) {
  return api.createPlotThread(gameId, title, description);
}

export async function gamesUpdatePlotThread(threadId: string, updates: Partial<PlotThreadUpdateRequest>) {
  return api.updatePlotThread(threadId, updates);
}

export async function gamesGetCharacters(gameId: string) {
  return api.getCharacters(gameId);
}

export async function gamesGetNPCs(gameId: string) {
  return api.getNPCs(gameId);
}

export async function gamesCreateNPC(gameId: string, name: string, description?: string) {
  return api.createNPC(gameId, name, description);
}

export async function gamesUpdateNPC(npcId: string, updates: Partial<NPCUpdateRequest>) {
  return api.updateNPC(npcId, updates);
}
