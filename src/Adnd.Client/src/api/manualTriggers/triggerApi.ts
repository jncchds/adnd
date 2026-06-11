import { api } from '../client';

export async function triggerNarrate(gameId: string) {
  return api.triggerNarrate(gameId);
}

export async function triggerSuggest(gameId: string) {
  return api.triggerSuggest(gameId);
}

export async function triggerConsistency(gameId: string) {
  return api.triggerConsistency(gameId);
}

export async function triggerReview(gameId: string) {
  return api.triggerReview(gameId);
}

export async function triggerFullReview(gameId: string, context?: string) {
  return api.triggerFullReview(gameId, context);
}

export async function triggerNewScene(gameId: string) {
  return api.triggerNewScene(gameId);
}

export async function triggerGMEvaluate(gameId: string) {
  return api.triggerGMEvaluate(gameId);
}

export async function triggerPlotCheck(gameId: string) {
  return api.triggerPlotCheck(gameId);
}

export async function triggerDetectOpportunities(gameId: string) {
  return api.triggerDetectOpportunities(gameId);
}

export async function triggerGenerateThreads(gameId: string) {
  return api.triggerGenerateThreads(gameId);
}

export async function triggerSpawnMilestones(gameId: string) {
  return api.triggerSpawnMilestones(gameId);
}

export async function triggerSessionSummary(gameId: string, sessionId?: string) {
  return api.triggerSessionSummary(gameId, sessionId);
}
