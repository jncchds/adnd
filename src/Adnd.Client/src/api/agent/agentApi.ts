import { api } from '../client';

export async function agentGetCallHistory(gameId: string, fromAgent?: number, action?: number, limit = 50) {
  const params = new URLSearchParams({ limit: String(limit) });
  if (fromAgent !== undefined) params.set('fromAgent', String(fromAgent));
  if (action !== undefined) params.set('action', String(action));
  return api.getAgentCallHistory(gameId, fromAgent, action, limit);
}

export async function agentGetCall(callId: string) {
  return api.getAgentCall(callId);
}

export async function agentGetPendingCalls(gameId: string) {
  return api.getPendingAgentCalls(gameId);
}

export async function agentCreateCall(gameId: string, fromAgent: number, toAgent: number, action: number, input?: string, sessionId?: string) {
  return api.createAgentCall(gameId, fromAgent, toAgent, action, input, sessionId);
}
