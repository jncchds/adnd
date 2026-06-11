import type { AuthUser, AuthResponse } from '../types/auth.types';
import type {
  GameListItem, GameDetail, InviteResponse, GMStatusResponse, SwayResponse,
  GameSessionListItem, GameSessionDetail, PlayerListItem, NPCListItem, NPCUpdateRequest,
  PlotThreadListItem, PlotThreadUpdateRequest, CharacterListItem, CharacterDetail,
  CharacterUpdateRequest,
} from '../types/game.types';
import type { PendingAgentCall } from '../types/agent.types';
import type { PlotThreadResponse, PlotReviewResponse, StoryOpportunityResponse } from '../types/plot.types';
import type {
  LLMPreset, LLMPresetDetail, LLMInteractionLog, LLMInteractionLogDetail,
  PresetUsageSummary, GameProviderUsageSummary, ProviderStatus,
  CreateLLMPresetRequest, UpdateLLMPresetRequest,
} from '../types/llm.types';
import type { ExecuteToolRequest, ExecuteToolResponse, DiceHistoryEntry,
  DiceHistoryResponse, CombatSummaryEntry, CombatLogResponse,
  SpellManagementResponse, SpellUpdateRequest, SessionNote, DiceStatsResponse, PlayerDiceStatsResponse,
  MessagePaginationResponse, MessageSearchResponse, GMToolResponse,
} from '../types/gm.types';
import type { GameTemplate, CreateGameTemplateRequest, UpdateGameTemplateRequest, PromptTemplate } from '../types/template.types';

const API_BASE = '/api';

class APIClient {
  private _token: string | null = localStorage.getItem('token');
  private _refreshToken: string | null = localStorage.getItem('refreshToken');
  private _user: string | null = localStorage.getItem('user');

  getToken() { return this._token; }
  setUser(user: AuthUser | null) {
    this._user = user ? JSON.stringify(user) : null;
    if (user) localStorage.setItem('user', this._user!);
    else localStorage.removeItem('user');
  }
  getUser(): AuthUser | null {
    if (!this._user) return null;
    try { return JSON.parse(this._user); } catch { return null; }
  }
  isAuthenticated(): boolean { return !!this._token; }
  setTokens(accessToken: string, refreshToken: string) {
    this._token = accessToken; this._refreshToken = refreshToken;
    localStorage.setItem('token', accessToken); localStorage.setItem('refreshToken', refreshToken);
  }
  clearTokens() {
    this._token = null; this._refreshToken = null;
    localStorage.removeItem('token'); localStorage.removeItem('refreshToken');
  }

  private async request<T>(url: string, options: RequestInit = {}): Promise<T> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this._token) headers['Authorization'] = `Bearer ${this._token}`;
    let response = await fetch(`${API_BASE}${url}`, { ...options, headers, credentials: 'include' });
    if ((response.status === 401 || response.status === 429) && this._refreshToken) {
      const refreshResponse = await fetch(`${API_BASE}/auth/refresh`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: this._refreshToken }),
      });
      if (refreshResponse.ok) {
        const data = await refreshResponse.json();
        this.setTokens(data.accessToken, data.refreshToken);
        headers['Authorization'] = `Bearer ${data.accessToken}`;
        response = await fetch(`${API_BASE}${url}`, { ...options, headers, credentials: 'include' });
      }
    }
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `HTTP ${response.status}`);
    }
    return response.json();
  }

  // Auth
  async register(e: string, p: string, d: string) { return this.request<AuthResponse>('/auth/register', { method: 'POST', body: JSON.stringify({ email: e, password: p, displayName: d }) }); }
  async login(e: string, p: string) { return this.request<AuthResponse>('/auth/login', { method: 'POST', body: JSON.stringify({ email: e, password: p }) }); }
  async logout() { if (this._refreshToken) await this.request('/auth/logout', { method: 'POST', body: JSON.stringify({ refreshToken: this._refreshToken }) }).catch(() => {}); this.clearTokens(); }
  async getMe() { return this.request<AuthUser>('/auth/me'); }
  async changePassword(c: string, n: string) { return this.request('/auth/change-password', { method: 'POST', body: JSON.stringify({ currentPassword: c, newPassword: n }) }); }
  async updateDisplayName(d: string) { return this.request('/auth/display-name', { method: 'PUT', body: JSON.stringify({ displayName: d }) }); }

  // Games
  async getGames() { return this.request<GameListItem[]>('/games'); }
  async getGame(id: string) { return this.request<GameDetail>('/games/' + id); }
  async createGame(n: string, s = 'dnd5e', v?: string, c?: string, l?: string, p?: string, g?: string, lang = 'English') { return this.request<GameDetail>('/games', { method: 'POST', body: JSON.stringify({ name: n, systemId: s, systemVersion: v, customSystemJson: c, llmPresetId: l, plotSeed: p, gameParameters: g, language: lang }) }); }
  async updateGameLanguage(g: string, l: string) { return this.request(`/games/${g}/language`, { method: 'PUT', body: JSON.stringify({ language: l }) }); }
  async deleteGame(id: string) { return this.request('/games/' + id, { method: 'DELETE' }); }
  async generateInvite(id: string) { return this.request<InviteResponse>('/games/' + id + '/invite', { method: 'POST' }); }
  async joinGame(id: string) { return this.request('/games/' + id + '/join', { method: 'POST' }); }
  async joinByCode(c: string) { return this.request('/games/join-by-code', { method: 'POST', body: JSON.stringify({ code: c }) }); }
  async leaveGame(id: string) { return this.request('/games/' + id + '/leave', { method: 'POST' }); }
  async getSessions(g: string) { return this.request<GameSessionListItem[]>(`/games/${g}/sessions`); }
  async createSession(g: string, t: string, d?: string) { return this.request<GameSessionDetail>(`/games/${g}/sessions`, { method: 'POST', body: JSON.stringify({ title: t, description: d }) }); }
  async closeSession(g: string, s: string) { return this.request(`/games/${g}/sessions/${s}/close`, { method: 'POST' }); }
  async getPlayers(g: string) { return this.request<PlayerListItem[]>(`/games/${g}/players`); }
  async promotePlayer(g: string, p: string, r: string) { return this.request(`/games/${g}/players/${p}/promote`, { method: 'POST', body: JSON.stringify(r) }); }
  async getActiveCombats(g: string) { return this.request(`/admin/games/${g}/combats`); }
  async startGame(g: string) { return this.request(`/admin/games/${g}/start`, { method: 'POST' }); }
  async archiveGame(g: string) { return this.request(`/admin/games/${g}/archive`, { method: 'POST' }); }
  async getGMStatus(g: string) { return this.request<GMStatusResponse>(`/admin/games/${g}/gm-status`); }
  async pauseGM(g: string) { return this.request(`/admin/games/${g}/gm/pause`, { method: 'POST' }); }
  async resumeGM(g: string) { return this.request(`/admin/games/${g}/gm/resume`, { method: 'POST' }); }
  async swayStory(g: string, d: string) { return this.request<SwayResponse>(`/admin/games/${g}/sway`, { method: 'POST', body: JSON.stringify({ direction: d }) }); }
  async pauseGame(g: string) { return this.request(`/admin/games/${g}/trigger/pause`, { method: 'POST' }); }
  async resumeGame(g: string) { return this.request(`/admin/games/${g}/trigger/resume`, { method: 'POST' }); }
  async triggerCombatStart(g: string) { return this.request(`/admin/games/${g}/trigger/combat-start`, { method: 'POST' }); }
  async triggerCombatEnd(g: string) { return this.request(`/admin/games/${g}/trigger/combat-end`, { method: 'POST' }); }
  async getWhispers(g: string, l = 50) { return this.request(`/admin/games/${g}/whispers?limit=${l}`); }
  async sendWhisper(g: string, s: string, t: string, tp: number, c: string) { return this.request(`/admin/games/${g}/whispers`, { method: 'POST', body: JSON.stringify({ sessionId: s, targets: t, type: tp, content: c }) }); }
  async sendGMWhisper(g: string, s: string, t: string[], tp: number, c: string) { return this.request(`/admin/games/${g}/whispers/gm?sessionId=${s}`, { method: 'POST', body: JSON.stringify({ targetPlayerIds: t, type: tp, content: c }) }); }

  // Admin
  async getNPCs(g: string) { return this.request<NPCListItem[]>(`/admin/games/${g}/npcs`); }
  async createNPC(g: string, n: string, d?: string) { return this.request('/admin/games/' + g + '/npcs', { method: 'POST', body: JSON.stringify({ name: n, description: d }) }); }
  async updateNPC(id: string, u: Partial<NPCUpdateRequest>) { return this.request('/admin/npcs/' + id, { method: 'PUT', body: JSON.stringify(u) }); }
  async deleteNPC(id: string) { return this.request('/admin/npcs/' + id, { method: 'DELETE' }); }
  async getPlotThreads(g: string) { return this.request<PlotThreadListItem[]>(`/admin/games/${g}/plot-threads`); }
  async createPlotThread(g: string, t: string, d: string) { return this.request('/admin/games/' + g + '/plot-threads', { method: 'POST', body: JSON.stringify({ title: t, description: d }) }); }
  async updatePlotThread(id: string, u: Partial<PlotThreadUpdateRequest>) { return this.request('/admin/plot-threads/' + id, { method: 'PUT', body: JSON.stringify(u) }); }
  async getPlotWeaverThreads(g: string) { return this.request<PlotThreadResponse[]>(`/admin/games/${g}/plot-threads`); }
  async triggerPlotReview(g: string, c?: string) { return this.request<PlotReviewResponse>(`/admin/games/${g}/plot-weaver/review`, { method: 'POST', body: JSON.stringify(c) }); }
  async getPlotReviewHistory(g: string, l = 20) { return this.request<PlotReviewResponse[]>(`/admin/games/${g}/plot-weaver/reviews?limit=${l}`); }
  async adjustThreadMomentum(g: string, t: string, d: number, r: string) { return this.request(`/admin/games/${g}/plot-weaver/threads/${t}/momentum`, { method: 'POST', body: JSON.stringify({ delta: d, reason: r }) }); }
  async detectOpportunities(g: string) { return this.request<StoryOpportunityResponse[]>(`/admin/games/${g}/plot-weaver/opportunities`, { method: 'POST' }); }
  async getCharacters(g: string) { return this.request<CharacterListItem[]>(`/admin/games/${g}/characters`); }
  async getCharacter(id: string) { return this.request<CharacterDetail>(`/admin/characters/${id}`); }
  async updateCharacter(id: string, u: Partial<CharacterUpdateRequest>) { return this.request('/admin/characters/' + id, { method: 'PUT', body: JSON.stringify(u) }); }
  async getGameState(g: string) { return this.request(`/admin/games/${g}/state`); }
  async updateGameState(g: string, gs?: string, ps?: string, gp?: string) { return this.request(`/admin/games/${g}/state`, { method: 'PUT', body: JSON.stringify({ gameState: gs, plotSeed: ps, gameParameters: gp }) }); }
  async getPlotContext(g: string, m = 20) { return this.request(`/admin/games/${g}/plot-context?maxMessages=${m}`); }
  async findSimilarThreads(g: string, q: string, l = 5) { return this.request('/admin/games/' + g + '/rag/similar-threads', { method: 'POST', body: JSON.stringify({ query: q, limit: l }) }); }
  async checkConsistency(g: string, m = 50) { return this.request(`/admin/games/${g}/rag/consistency?messageCount=${m}`); }
  async getSessionSummary(g: string, s: string, m = 30) { return this.request(`/admin/games/${g}/rag/summary?sessionId=${s}&messageCount=${m}`); }
  async getSystems() { return this.request('/admin/systems'); }

  // LLM
  async getLLMProviders() { return this.request<ProviderStatus[]>('/admin/llm/providers'); }
  async getLLMPresets() { return this.request<LLMPreset[]>('/admin/llm-presets'); }
  async getLLMPreset(id: string) { return this.request<LLMPresetDetail>(`/admin/llm-presets/${id}`); }
  async createLLMPreset(r: CreateLLMPresetRequest) { return this.request<LLMPresetDetail>('/admin/llm-presets', { method: 'POST', body: JSON.stringify(r) }); }
  async updateLLMPreset(id: string, r: UpdateLLMPresetRequest) { return this.request<LLMPresetDetail>(`/admin/llm-presets/${id}`, { method: 'PUT', body: JSON.stringify(r) }); }
  async deleteLLMPreset(id: string) { return this.request(`/admin/llm-presets/${id}`, { method: 'DELETE' }); }
  async testLLMPreset(id: string) { return this.request(`/admin/llm-presets/${id}/test`, { method: 'POST' }); }
  async setDefaultPreset(id: string) { return this.request(`/admin/llm-presets/${id}/set-default`, { method: 'POST' }); }
  async getProviderModels(pt: string, eu?: string, ak?: string) {
    const p = new URLSearchParams({ providerType: pt });
    if (eu) p.set('endpointUrl', eu); if (ak) p.set('apiKey', ak);
    return this.request<string[]>(`/admin/llm-presets/models?${p}`);
  }

  // LLM Logs
  async getLLMInteractions(params?: { presetId?: string; gameId?: string; providerType?: string; from?: string; to?: string; limit?: number }) {
    const s = new URLSearchParams();
    if (params?.presetId) s.set('presetId', params.presetId);
    if (params?.gameId) s.set('gameId', params.gameId);
    if (params?.providerType) s.set('providerType', params.providerType);
    if (params?.from) s.set('from', params.from);
    if (params?.to) s.set('to', params.to);
    if (params?.limit) s.set('limit', String(params.limit));
    return this.request<LLMInteractionLog[]>(`/admin/llm-interactions?${s}`);
  }
  async getLLMInteraction(id: string) { return this.request<LLMInteractionLogDetail>(`/admin/llm-interactions/${id}`); }
  async deleteLLMInteraction(id: string) { return this.request(`/admin/llm-interactions/${id}`, { method: 'DELETE' }); }
  async getPresetUsage(from?: string, to?: string) {
    const s = new URLSearchParams();
    if (from) s.set('from', from); if (to) s.set('to', to);
    return this.request<PresetUsageSummary[]>(`/admin/llm-interactions/preset-usage?${s}`);
  }
  async cleanupOldLogs(b: string) { return this.request('/admin/llm-interactions/cleanup', { method: 'POST', body: JSON.stringify(b) }); }
  async getGameProviderUsage(g: string, from?: string, to?: string) {
    const s = new URLSearchParams();
    if (from) s.set('from', from); if (to) s.set('to', to);
    return this.request<GameProviderUsageSummary[]>(`/admin/games/${g}/llm-provider-usage?${s}`);
  }

  // Agent
  async getAgentCallHistory(g: string, f?: number, a?: number, l = 50) {
    const p = new URLSearchParams({ limit: String(l) });
    if (f !== undefined) p.set('fromAgent', String(f));
    if (a !== undefined) p.set('action', String(a));
    return this.request(`/admin/games/${g}/agent-calls?${p}`);
  }
  async getAgentCall(id: string) { return this.request(`/admin/agent-calls/${id}`); }
  async getPendingAgentCalls(g: string) { return this.request<{ pendingCalls: PendingAgentCall[]; pendingCount: number; runningCount: number }>(`/admin/games/${g}/agent-calls/pending`); }
  async createAgentCall(g: string, f: number, t: number, a: number, i?: string, s?: string) { return this.request(`/admin/games/${g}/agent-calls`, { method: 'POST', body: JSON.stringify({ fromAgent: f, toAgent: t, action: a, input: i, sessionId: s }) }); }

  // Manual Triggers
  async triggerNarrate(g: string) { return this.request(`/admin/games/${g}/trigger/narrate`, { method: 'POST' }); }
  async triggerSuggest(g: string) { return this.request(`/admin/games/${g}/trigger/suggest`, { method: 'POST' }); }
  async triggerConsistency(g: string) { return this.request(`/admin/games/${g}/trigger/consistency`, { method: 'POST' }); }
  async triggerReview(g: string) { return this.request(`/admin/games/${g}/trigger/review`, { method: 'POST' }); }
  async triggerFullReview(g: string, c?: string) { return this.request(`/admin/games/${g}/trigger/full-review`, { method: 'POST', body: JSON.stringify(c) }); }
  async triggerNewScene(g: string) { return this.request(`/admin/games/${g}/trigger/new-scene`, { method: 'POST' }); }
  async triggerGMEvaluate(g: string) { return this.request(`/admin/games/${g}/trigger/gm-evaluate`, { method: 'POST' }); }
  async triggerPlotCheck(g: string) { return this.request(`/admin/games/${g}/trigger/plot-check`, { method: 'POST' }); }
  async triggerDetectOpportunities(g: string) { return this.request(`/admin/games/${g}/trigger/detect-opportunities`, { method: 'POST' }); }
  async triggerGenerateThreads(g: string) { return this.request(`/admin/games/${g}/trigger/generate-threads`, { method: 'POST' }); }
  async triggerSpawnMilestones(g: string) { return this.request(`/admin/games/${g}/trigger/spawn-milestones`, { method: 'POST' }); }
  async triggerSessionSummary(g: string, s?: string) { return this.request(`/admin/games/${g}/trigger/session-summary`, { method: 'POST', body: JSON.stringify(s) }); }

  // Templates
  async getGameTemplates() { return this.request<GameTemplate[]>('/admin/game-templates'); }
  async createGameTemplate(r: CreateGameTemplateRequest) { return this.request<GameTemplate>('/admin/game-templates', { method: 'POST', body: JSON.stringify(r) }); }
  async updateGameTemplate(id: string, r: UpdateGameTemplateRequest) { return this.request<GameTemplate>(`/admin/game-templates/${id}`, { method: 'PUT', body: JSON.stringify(r) }); }
  async deleteGameTemplate(id: string) { return this.request(`/admin/game-templates/${id}`, { method: 'DELETE' }); }
  async getGameTemplate(id: string) { return this.request<GameTemplate>(`/admin/game-templates/${id}`); }

  // GM Tools
  async getGMTools(g: string) { return this.request<GMToolResponse>(`/admin/games/${g}/tools`); }
  async getGMToolsByCategory(g: string, c: string) { return this.request<GMToolResponse>(`/admin/games/${g}/tools/${c}`); }
  async executeGMTool(g: string, s: string, r: ExecuteToolRequest) { return this.request<ExecuteToolResponse>(`/admin/games/${g}/tools/execute?sessionId=${s}`, { method: 'POST', body: JSON.stringify(r) }); }

  // Tool Calls
  async getPendingToolCalls(g: string) { return this.request(`/admin/games/${g}/tool-calls/pending`); }
  async confirmToolCall(g: string, t: string, a: boolean) { return this.request(`/admin/games/${g}/tool-calls/${t}/confirm`, { method: 'POST', body: JSON.stringify({ approved: a }) }); }
  async confirmPlayerRoll(g: string, t: string) { return this.request(`/admin/games/${g}/tool-calls/${t}/confirm-roll`, { method: 'POST' }); }
  async declinePlayerRoll(g: string, t: string) { return this.request(`/admin/games/${g}/tool-calls/${t}/decline`, { method: 'POST' }); }

  // Dice
  async getDiceHistory(g: string, p?: { sessionId?: string; playerId?: string; limit?: number; sortBy?: string }) {
    const s = new URLSearchParams();
    if (p?.sessionId) s.set('sessionId', p.sessionId);
    if (p?.playerId) s.set('playerId', p.playerId);
    if (p?.limit) s.set('limit', String(p.limit));
    if (p?.sortBy) s.set('sortBy', p.sortBy);
    return this.request<DiceHistoryResponse>(`/admin/games/${g}/dice-history?${s}`);
  }
  async getDiceRoll(g: string, m: string) { return this.request<DiceHistoryEntry>(`/admin/games/${g}/dice-history/${m}`); }
  async getDiceStats(g: string, s?: string) {
    const p = new URLSearchParams(); if (s) p.set('sessionId', s);
    return this.request<DiceStatsResponse>(`/admin/games/${g}/dice-stats?${p}`);
  }
  async getPlayerDiceStats(g: string, p: string, s?: string) {
    const ps = new URLSearchParams(); if (s) ps.set('sessionId', s);
    return this.request<PlayerDiceStatsResponse>(`/admin/games/${g}/dice-stats/player/${p}?${ps}`);
  }

  // Combat
  async getCombats(g: string, l = 50) { return this.request<{ gameId: string; count: number; combats: CombatSummaryEntry[] }>(`/admin/games/${g}/combats?limit=${l}`); }
  async getCombat(g: string, c: string) { return this.request<CombatLogResponse>(`/admin/games/${g}/combats/${c}`); }

  // Spells
  async getCharacterSpells(id: string) { return this.request<SpellManagementResponse>(`/admin/characters/${id}/spells`); }
  async updateCharacterSpells(id: string, s: SpellUpdateRequest[]) { return this.request(`/admin/characters/${id}/spells`, { method: 'PUT', body: JSON.stringify(s) }); }
  async updateSingleSpell(id: string, n: string, s: SpellUpdateRequest) { return this.request(`/admin/characters/${id}/spells/${encodeURIComponent(n)}`, { method: 'PUT', body: JSON.stringify(s) }); }
  async removeSpell(id: string, n: string) { return this.request(`/admin/characters/${id}/spells/${encodeURIComponent(n)}`, { method: 'DELETE' }); }

  // Session Notes
  async getSessionNotes(g: string, s: string) { return this.request<{ gameId: string; sessionId: string; notes: SessionNote[] }>(`/admin/games/${g}/sessions/${s}/notes`); }
  async createSessionNote(g: string, s: string, t: string, c: string) { return this.request<SessionNote>(`/admin/games/${g}/sessions/${s}/notes`, { method: 'POST', body: JSON.stringify({ title: t, content: c }) }); }
  async updateSessionNote(g: string, n: string, t: string, c: string) { return this.request<SessionNote>(`/admin/games/${g}/notes/${n}`, { method: 'PUT', body: JSON.stringify({ title: t, content: c }) }); }
  async deleteSessionNote(g: string, n: string) { return this.request<{ message: string }>(`/admin/games/${g}/notes/${n}`, { method: 'DELETE' }); }

  // Messages
  async getMessagesPaginated(g: string, s: string, p = 1, ps = 50, t?: number, a?: string) {
    const params = new URLSearchParams({ page: String(p), pageSize: String(ps) });
    if (t !== undefined) params.set('type', String(t));
    if (a) params.set('anchorId', a);
    return this.request<MessagePaginationResponse>(`/admin/games/${g}/sessions/${s}/messages?${params}`);
  }
  async searchMessages(g: string, s: string, q: string, qe: number[], l = 10) { return this.request<MessageSearchResponse>(`/admin/games/${g}/sessions/${s}/messages/search`, { method: 'POST', body: JSON.stringify({ query: q, queryEmbedding: qe, limit: l }) }); }

  // Prompt Templates
  async getPromptTemplates(g: string) { return this.request<PromptTemplate[]>(`/admin/games/${g}/prompt-templates`); }
  async createPromptTemplate(g: string, n: string, t: string, p: string) { return this.request<PromptTemplate>(`/admin/games/${g}/prompt-templates`, { method: 'POST', body: JSON.stringify({ name: n, type: t, prompt: p }) }); }
  async updatePromptTemplate(g: string, id: string, n: string, t: string, p: string, ia?: boolean, idf?: boolean) { return this.request<PromptTemplate>(`/admin/games/${g}/prompt-templates/${id}`, { method: 'PUT', body: JSON.stringify({ name: n, type: t, prompt: p, isActive: ia, isDefault: idf }) }); }
  async deletePromptTemplate(g: string, id: string) { return this.request<{ message: string }>(`/admin/games/${g}/prompt-templates/${id}`, { method: 'DELETE' }); }
  async getDefaultTemplate(g: string, t: string) { return this.request<PromptTemplate | null>(`/admin/games/${g}/prompt-templates/default/${encodeURIComponent(t)}`); }
}

export const api = new APIClient();
