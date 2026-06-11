import { api } from '../client';

export async function combatGetCombats(gameId: string, limit = 50) {
  return api.getCombats(gameId, limit);
}

export async function combatGetCombat(gameId: string, combatId: string) {
  return api.getCombat(gameId, combatId);
}
