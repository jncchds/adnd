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

  getToken() {
    return this._token;
  }

  setUser(user: AuthUser | null) {
    this._user = user ? JSON.stringify(user) : null;
    if (user) {
      localStorage.setItem('user', this._user!);
    } else {
      localStorage.removeItem('user');
    }
  }

  getUser(): AuthUser | null {
    if (!this._user) return null;
    try {
      return JSON.parse(this._user);
    } catch {
      return null;
    }
  }

  isAuthenticated(): boolean {
    return !!this._token;
  }

  setTokens(accessToken: string, refreshToken: string) {
    this._token = accessToken;
    this._refreshToken = refreshToken;
    localStorage.setItem('token', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
  }

  clearTokens() {
    this._token = null;
    this._refreshToken = null;
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
  }

  private async request<T>(
    url: string,
    options: RequestInit = {}
  ): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (this._token) {
      headers['Authorization'] = `Bearer ${this._token}`;
    }

    let response = await fetch(`${API_BASE}${url}`, {
      ...options,
      headers,
      credentials: 'include',
    });

    // If unauthorized or rate-limited, try to refresh token
    if ((response.status === 401 || response.status === 429) && this._refreshToken) {
      const refreshResponse = await fetch(`${API_BASE}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: this._refreshToken }),
      });

      if (refreshResponse.ok) {
        const data = await refreshResponse.json();
        this.setTokens(data.accessToken, data.refreshToken);
        headers['Authorization'] = `Bearer ${data.accessToken}`;

        response = await fetch(`${API_BASE}${url}`, {
          ...options,
          headers,
          credentials: 'include',
        });
      }
    }

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `HTTP ${response.status}`);
    }

    return response.json();
  }

  // ==================== Auth ====================
  async register(email: string, password: string, displayName: string) {
    return this.request<AuthResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, displayName }),
    });
  }

  async login(email: string, password: string) {
    return this.request<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
  }

  async logout() {
    if (this._refreshToken) {
      await this.request('/auth/logout', {
        method: 'POST',
        body: JSON.stringify({ refreshToken: this._refreshToken }),
      }).catch(() => {});
    }
    this.clearTokens();
  }

  async getMe() {
    return this.request<AuthUser>('/auth/me');
  }

  async changePassword(currentPassword: string, newPassword: string) {
    return this.request('/auth/change-password', {
      method: 'POST',
      body: JSON.stringify({ currentPassword, newPassword }),
    });
  }

  async updateDisplayName(displayName: string) {
    return this.request('/auth/display-name', {
      method: 'PUT',
      body: JSON.stringify({ displayName }),
    });
  }

  // ==================== Games ====================
  async getGames() {
    return this.request<GameListItem[]>('/games');
  }

  async getGame(id: string) {
    return this.request<GameDetail>('/games/' + id);
  }

  async createGame(
    name: string,
    systemId = 'dnd5e',
    systemVersion?: string,
    customSystemJson?: string,
    llmPresetId?: string,
    plotSeed?: string,
    gameParameters?: string,
    language = 'English'
  ) {
    return this.request<GameDetail>('/games', {
      method: 'POST',
      body: JSON.stringify({ name, systemId, systemVersion, customSystemJson, llmPresetId, plotSeed, gameParameters, language }),
    });
  }

  async updateGameLanguage(gameId: string, language: string) {
    return this.request(`/games/${gameId}/language`, {
      method: 'PUT',
      body: JSON.stringify({ language }),
    });
  }

  async deleteGame(id: string) {
    return this.request('/games/' + id, { method: 'DELETE' });
  }

  async generateInvite(id: string) {
    return this.request<InviteResponse>('/games/' + id + '/invite', { method: 'POST' });
  }

  async joinGame(id: string) {
    return this.request('/games/' + id + '/join', { method: 'POST' });
  }

  async joinByCode(code: string) {
    return this.request('/games/join-by-code', {
      method: 'POST',
      body: JSON.stringify({ code }),
    });
  }

  async leaveGame(id: string) {
    return this.request('/games/' + id + '/leave', { method: 'POST' });
  }

  // ==================== Combat ====================
  async getActiveCombats(gameId: string) {
    return this.request(`/admin/games/${gameId}/combats`);
  }

  // ==================== Sessions ====================
  async getSessions(gameId: string) {
    return this.request<GameSessionListItem[]>(`/games/${gameId}/sessions`);
  }

  async createSession(gameId: string, title: string, description?: string) {
    return this.request<GameSessionDetail>(`/games/${gameId}/sessions`, {
      method: 'POST',
      body: JSON.stringify({ title, description }),
    });
  }

  async closeSession(gameId: string, sessionId: string) {
    return this.request(`/games/${gameId}/sessions/${sessionId}/close`, { method: 'POST' });
  }

  // ==================== Players ====================
  async getPlayers(gameId: string) {
    return this.request<PlayerListItem[]>(`/games/${gameId}/players`);
  }

  async promotePlayer(gameId: string, playerId: string, role: string) {
    return this.request(`/games/${gameId}/players/${playerId}/promote`, {
      method: 'POST',
      body: JSON.stringify(role),
    });
  }

  // ==================== Admin ====================
  async getNPCs(gameId: string) {
    return this.request<NPCListItem[]>(`/admin/games/${gameId}/npcs`);
  }

  async createNPC(gameId: string, name: string, description?: string) {
    return this.request('/admin/games/' + gameId + '/npcs', {
      method: 'POST',
      body: JSON.stringify({ name, description }),
    });
  }

  async updateNPC(npcId: string, updates: Partial<NPCUpdateRequest>) {
    return this.request('/admin/npcs/' + npcId, {
      method: 'PUT',
      body: JSON.stringify(updates),
    });
  }

  async deleteNPC(npcId: string) {
    return this.request('/admin/npcs/' + npcId, { method: 'DELETE' });
  }

  async createPlotThread(gameId: string, title: string, description: string) {
    return this.request('/admin/games/' + gameId + '/plot-threads', {
      method: 'POST',
      body: JSON.stringify({ title, description }),
    });
  }

  async updatePlotThread(threadId: string, updates: Partial<PlotThreadUpdateRequest>) {
    return this.request('/admin/plot-threads/' + threadId, {
      method: 'PUT',
      body: JSON.stringify(updates),
    });
  }

  async getPlotThreads(gameId: string) {
    return this.request<PlotThreadListItem[]>(`/admin/games/${gameId}/plot-threads`);
  }

  // ==================== PlotWeaver ====================
  async getPlotWeaverThreads(gameId: string) {
    return this.request<PlotThreadResponse[]>(`/admin/games/${gameId}/plot-threads`);
  }

  async triggerPlotReview(gameId: string, context?: string) {
    return this.request<PlotReviewResponse>(`/admin/games/${gameId}/plot-weaver/review`, {
      method: 'POST',
      body: JSON.stringify(context),
    });
  }

  async getPlotReviewHistory(gameId: string, limit = 20) {
    return this.request<PlotReviewResponse[]>(`/admin/games/${gameId}/plot-weaver/reviews?limit=${limit}`);
  }

  async adjustThreadMomentum(gameId: string, threadId: string, delta: number, reason: string) {
    return this.request(`/admin/games/${gameId}/plot-weaver/threads/${threadId}/momentum`, {
      method: 'POST',
      body: JSON.stringify({ delta, reason }),
    });
  }

  async detectOpportunities(gameId: string) {
    return this.request<StoryOpportunityResponse[]>(`/admin/games/${gameId}/plot-weaver/opportunities`, {
      method: 'POST',
    });
  }

  async getCharacters(gameId: string) {
    return this.request<CharacterListItem[]>(`/admin/games/${gameId}/characters`);
  }

  async getCharacter(characterId: string) {
    return this.request<CharacterDetail>(`/admin/characters/${characterId}`);
  }

  async updateCharacter(characterId: string, updates: Partial<CharacterUpdateRequest>) {
    return this.request('/admin/characters/' + characterId, {
      method: 'PUT',
      body: JSON.stringify(updates),
    });
  }

  async startGame(gameId: string) {
    return this.request(`/admin/games/${gameId}/start`, { method: 'POST' });
  }

  async archiveGame(gameId: string) {
    return this.request(`/admin/games/${gameId}/archive`, { method: 'POST' });
  }

  async getGMStatus(gameId: string) {
    return this.request<GMStatusResponse>(`/admin/games/${gameId}/gm-status`);
  }

  async pauseGM(gameId: string) {
    return this.request(`/admin/games/${gameId}/gm/pause`, { method: 'POST' });
  }

  async resumeGM(gameId: string) {
    return this.request(`/admin/games/${gameId}/gm/resume`, { method: 'POST' });
  }

  async swayStory(gameId: string, direction: string) {
    return this.request<SwayResponse>(`/admin/games/${gameId}/sway`, {
      method: 'POST',
      body: JSON.stringify({ direction }),
    });
  }

  async pauseGame(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/pause`, { method: 'POST' });
  }

  async resumeGame(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/resume`, { method: 'POST' });
  }

  async triggerCombatStart(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/combat-start`, { method: 'POST' });
  }

  async triggerCombatEnd(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/combat-end`, { method: 'POST' });
  }

  // ==================== LLM / RAG ====================

  async getLLMProviders() {
    return this.request<ProviderStatus[]>('/admin/llm/providers');
  }

  async getPlotContext(gameId: string, maxMessages = 20) {
    return this.request(`/admin/games/${gameId}/plot-context?maxMessages=${maxMessages}`);
  }

  async findSimilarThreads(gameId: string, query: string, limit = 5) {
    return this.request('/admin/games/' + gameId + '/rag/similar-threads', {
      method: 'POST',
      body: JSON.stringify({ query, limit }),
    });
  }

  async checkConsistency(gameId: string, messageCount = 50) {
    return this.request(`/admin/games/${gameId}/rag/consistency?messageCount=${messageCount}`);
  }

  async getSessionSummary(gameId: string, sessionId: string, messageCount = 30) {
    return this.request(`/admin/games/${gameId}/rag/summary?sessionId=${sessionId}&messageCount=${messageCount}`);
  }

  // ==================== Systems ====================
  async getSystems() {
    return this.request('/admin/systems');
  }

  // ==================== Whispers ====================
  async getWhispers(gameId: string, limit = 50) {
    return this.request(`/admin/games/${gameId}/whispers?limit=${limit}`);
  }

  async sendWhisper(gameId: string, sessionId: string, targets: string, type: number, content: string) {
    return this.request(`/admin/games/${gameId}/whispers`, {
      method: 'POST',
      body: JSON.stringify({ sessionId, targets, type, content }),
    });
  }

  async sendGMWhisper(gameId: string, sessionId: string, targetPlayerIds: string[], type: number, content: string) {
    return this.request(`/admin/games/${gameId}/whispers/gm?sessionId=${sessionId}`, {
      method: 'POST',
      body: JSON.stringify({ targetPlayerIds, type, content }),
    });
  }

  // ==================== Agent Framework ====================
  async getAgentCallHistory(gameId: string, fromAgent?: number, action?: number, limit = 50) {
    const params = new URLSearchParams({ limit: String(limit) });
    if (fromAgent !== undefined) params.set('fromAgent', String(fromAgent));
    if (action !== undefined) params.set('action', String(action));
    return this.request(`/admin/games/${gameId}/agent-calls?${params}`);
  }

  async getAgentCall(callId: string) {
    return this.request(`/admin/agent-calls/${callId}`);
  }

  async getPendingAgentCalls(gameId: string) {
    return this.request<{ pendingCalls: PendingAgentCall[]; pendingCount: number; runningCount: number }>(
      `/admin/games/${gameId}/agent-calls/pending`
    );
  }

  // ==================== Manual LLM Triggers ====================
  async triggerNarrate(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/narrate`, { method: 'POST' });
  }

  async triggerSuggest(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/suggest`, { method: 'POST' });
  }

  async triggerConsistency(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/consistency`, { method: 'POST' });
  }

  async triggerReview(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/review`, { method: 'POST' });
  }

  async triggerFullReview(gameId: string, context?: string) {
    return this.request(`/admin/games/${gameId}/trigger/full-review`, {
      method: 'POST',
      body: JSON.stringify(context),
    });
  }

  async triggerNewScene(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/new-scene`, { method: 'POST' });
  }

  async triggerGMEvaluate(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/gm-evaluate`, { method: 'POST' });
  }

  async triggerPlotCheck(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/plot-check`, { method: 'POST' });
  }

  async triggerDetectOpportunities(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/detect-opportunities`, { method: 'POST' });
  }

  async triggerGenerateThreads(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/generate-threads`, { method: 'POST' });
  }

  async triggerSpawnMilestones(gameId: string) {
    return this.request(`/admin/games/${gameId}/trigger/spawn-milestones`, { method: 'POST' });
  }

  async triggerSessionSummary(gameId: string, sessionId?: string) {
    return this.request(`/admin/games/${gameId}/trigger/session-summary`, {
      method: 'POST',
      body: JSON.stringify(sessionId),
    });
  }

  async createAgentCall(gameId: string, fromAgent: number, toAgent: number, action: number, input?: string, sessionId?: string) {
    return this.request(`/admin/games/${gameId}/agent-calls`, {
      method: 'POST',
      body: JSON.stringify({ fromAgent, toAgent, action, input, sessionId }),
    });
  }

  // ==================== Game State ====================
  async getGameState(gameId: string) {
    return this.request(`/admin/games/${gameId}/state`);
  }

  async updateGameState(gameId: string, gameState?: string, plotSeed?: string, gameParameters?: string) {
    return this.request(`/admin/games/${gameId}/state`, {
      method: 'PUT',
      body: JSON.stringify({ gameState, plotSeed, gameParameters }),
    });
  }

  // ==================== LLM Presets ====================
  async getLLMPresets() {
    return this.request<LLMPreset[]>('/admin/llm-presets');
  }

  async getLLMPreset(presetId: string) {
    return this.request<LLMPresetDetail>(`/admin/llm-presets/${presetId}`);
  }

  async createLLMPreset(request: CreateLLMPresetRequest) {
    return this.request<LLMPresetDetail>('/admin/llm-presets', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async updateLLMPreset(presetId: string, request: UpdateLLMPresetRequest) {
    return this.request<LLMPresetDetail>(`/admin/llm-presets/${presetId}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
  }

  async deleteLLMPreset(presetId: string) {
    return this.request(`/admin/llm-presets/${presetId}`, { method: 'DELETE' });
  }

  async testLLMPreset(presetId: string) {
    return this.request(`/admin/llm-presets/${presetId}/test`, {
      method: 'POST',
    });
  }

  async setDefaultPreset(presetId: string) {
    return this.request(`/admin/llm-presets/${presetId}/set-default`, {
      method: 'POST',
    });
  }

  // ==================== Game Templates ====================

  async getGameTemplates() {
    return this.request<GameTemplate[]>('/admin/game-templates');
  }

  async createGameTemplate(request: CreateGameTemplateRequest) {
    return this.request<GameTemplate>('/admin/game-templates', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async updateGameTemplate(id: string, request: UpdateGameTemplateRequest) {
    return this.request<GameTemplate>(`/admin/game-templates/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
  }

  async deleteGameTemplate(id: string) {
    return this.request(`/admin/game-templates/${id}`, { method: 'DELETE' });
  }

  async getGameTemplate(id: string) {
    return this.request<GameTemplate>(`/admin/game-templates/${id}`);
  }

  async getProviderModels(providerType: string, endpointUrl?: string, apiKey?: string) {
    const params = new URLSearchParams({ providerType });
    if (endpointUrl) params.set('endpointUrl', endpointUrl);
    if (apiKey) params.set('apiKey', apiKey);
    return this.request<string[]>(`/admin/llm-presets/models?${params}`);
  }

  // ==================== LLM Interaction Logs ====================
  async getLLMInteractions(params?: {
    presetId?: string;
    gameId?: string;
    providerType?: string;
    from?: string;
    to?: string;
    limit?: number;
  }) {
    const searchParams = new URLSearchParams();
    if (params?.presetId) searchParams.set('presetId', params.presetId);
    if (params?.gameId) searchParams.set('gameId', params.gameId);
    if (params?.providerType) searchParams.set('providerType', params.providerType);
    if (params?.from) searchParams.set('from', params.from);
    if (params?.to) searchParams.set('to', params.to);
    if (params?.limit) searchParams.set('limit', String(params.limit));
    return this.request<LLMInteractionLog[]>(`/admin/llm-interactions?${searchParams}`);
  }

  async getLLMInteraction(logId: string) {
    return this.request<LLMInteractionLogDetail>(`/admin/llm-interactions/${logId}`);
  }

  async deleteLLMInteraction(logId: string) {
    return this.request(`/admin/llm-interactions/${logId}`, { method: 'DELETE' });
  }

  async getPresetUsage(from?: string, to?: string) {
    const searchParams = new URLSearchParams();
    if (from) searchParams.set('from', from);
    if (to) searchParams.set('to', to);
    return this.request<PresetUsageSummary[]>(`/admin/llm-interactions/preset-usage?${searchParams}`);
  }

  async cleanupOldLogs(before: string) {
    return this.request('/admin/llm-interactions/cleanup', {
      method: 'POST',
      body: JSON.stringify(before),
    });
  }

  async getGameProviderUsage(gameId: string, from?: string, to?: string) {
    const searchParams = new URLSearchParams();
    if (from) searchParams.set('from', from);
    if (to) searchParams.set('to', to);
    return this.request<GameProviderUsageSummary[]>(`/admin/games/${gameId}/llm-provider-usage?${searchParams}`);
  }

  // ==================== GM Tool Calls ====================
  async getPendingToolCalls(gameId: string) {
    return this.request(`/admin/games/${gameId}/tool-calls/pending`);
  }

  async confirmToolCall(gameId: string, toolCallId: string, approved: boolean) {
    return this.request(`/admin/games/${gameId}/tool-calls/${toolCallId}/confirm`, {
      method: 'POST',
      body: JSON.stringify({ approved }),
    });
  }

  async confirmPlayerRoll(gameId: string, toolCallId: string) {
    return this.request(`/admin/games/${gameId}/tool-calls/${toolCallId}/confirm-roll`, {
      method: 'POST',
    });
  }

  async declinePlayerRoll(gameId: string, toolCallId: string) {
    return this.request(`/admin/games/${gameId}/tool-calls/${toolCallId}/decline`, {
      method: 'POST',
    });
  }

  // ==================== Dice History ====================
  async getDiceHistory(gameId: string, params?: {
    sessionId?: string;
    playerId?: string;
    limit?: number;
    sortBy?: string;
  }) {
    const searchParams = new URLSearchParams();
    if (params?.sessionId) searchParams.set('sessionId', params.sessionId);
    if (params?.playerId) searchParams.set('playerId', params.playerId);
    if (params?.limit) searchParams.set('limit', String(params.limit));
    if (params?.sortBy) searchParams.set('sortBy', params.sortBy);
    return this.request<DiceHistoryResponse>(`/admin/games/${gameId}/dice-history?${searchParams}`);
  }

  async getDiceRoll(gameId: string, messageId: string) {
    return this.request<DiceHistoryEntry>(`/admin/games/${gameId}/dice-history/${messageId}`);
  }

  // ==================== Combat Log ====================
  async getCombats(gameId: string, limit = 50) {
    return this.request<{ gameId: string; count: number; combats: CombatSummaryEntry[] }>(
      `/admin/games/${gameId}/combats?limit=${limit}`
    );
  }

  async getCombat(gameId: string, combatId: string) {
    return this.request<CombatLogResponse>(`/admin/games/${gameId}/combats/${combatId}`);
  }

  // ==================== GM Tools ====================
  async getGMTools(gameId: string) {
    return this.request<GMToolResponse>(`/admin/games/${gameId}/tools`);
  }

  async getGMToolsByCategory(gameId: string, category: string) {
    return this.request<GMToolResponse>(`/admin/games/${gameId}/tools/${category}`);
  }

  async executeGMTool(gameId: string, sessionId: string, request: ExecuteToolRequest) {
    return this.request<ExecuteToolResponse>(
      `/admin/games/${gameId}/tools/execute?sessionId=${sessionId}`,
      {
        method: 'POST',
        body: JSON.stringify(request),
      }
    );
  }

  // ==================== Spell Management ====================
  async getCharacterSpells(characterId: string) {
    return this.request<SpellManagementResponse>(`/admin/characters/${characterId}/spells`);
  }

  async updateCharacterSpells(characterId: string, spells: SpellUpdateRequest[]) {
    return this.request(`/admin/characters/${characterId}/spells`, {
      method: 'PUT',
      body: JSON.stringify(spells),
    });
  }

  async updateSingleSpell(characterId: string, spellName: string, spell: SpellUpdateRequest) {
    return this.request(`/admin/characters/${characterId}/spells/${encodeURIComponent(spellName)}`, {
      method: 'PUT',
      body: JSON.stringify(spell),
    });
  }

  async removeSpell(characterId: string, spellName: string) {
    return this.request(`/admin/characters/${characterId}/spells/${encodeURIComponent(spellName)}`, {
      method: 'DELETE',
    });
  }

  // ==================== Session Notes ====================

  async getSessionNotes(gameId: string, sessionId: string) {
    return this.request<{ gameId: string; sessionId: string; notes: SessionNote[] }>(`/admin/games/${gameId}/sessions/${sessionId}/notes`);
  }

  async createSessionNote(gameId: string, sessionId: string, title: string, content: string) {
    return this.request<SessionNote>(`/admin/games/${gameId}/sessions/${sessionId}/notes`, {
      method: 'POST',
      body: JSON.stringify({ title, content }),
    });
  }

  async updateSessionNote(gameId: string, noteId: string, title: string, content: string) {
    return this.request<SessionNote>(`/admin/games/${gameId}/notes/${noteId}`, {
      method: 'PUT',
      body: JSON.stringify({ title, content }),
    });
  }

  async deleteSessionNote(gameId: string, noteId: string) {
    return this.request<{ message: string }>(`/admin/games/${gameId}/notes/${noteId}`, { method: 'DELETE' });
  }

  // ==================== Dice Statistics ====================

  async getDiceStats(gameId: string, sessionId?: string) {
    const params = new URLSearchParams();
    if (sessionId) params.set('sessionId', sessionId);
    return this.request<DiceStatsResponse>(`/admin/games/${gameId}/dice-stats?${params}`);
  }

  async getPlayerDiceStats(gameId: string, playerId: string, sessionId?: string) {
    const params = new URLSearchParams();
    if (sessionId) params.set('sessionId', sessionId);
    return this.request<PlayerDiceStatsResponse>(`/admin/games/${gameId}/dice-stats/player/${playerId}?${params}`);
  }

  // ==================== Messages (paginated) ====================

  async getMessagesPaginated(gameId: string, sessionId: string, page = 1, pageSize = 50, type?: number, anchorId?: string) {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (type !== undefined) params.set('type', String(type));
    if (anchorId) params.set('anchorId', anchorId);
    return this.request<MessagePaginationResponse>(`/admin/games/${gameId}/sessions/${sessionId}/messages?${params}`);
  }

  // ==================== Message Search ====================

  async searchMessages(gameId: string, sessionId: string, query: string, queryEmbedding: number[], limit = 10) {
    return this.request<MessageSearchResponse>(`/admin/games/${gameId}/sessions/${sessionId}/messages/search`, {
      method: 'POST',
      body: JSON.stringify({ query, queryEmbedding, limit }),
    });
  }

  // ==================== Prompt Templates ====================

  async getPromptTemplates(gameId: string) {
    return this.request<PromptTemplate[]>(`/admin/games/${gameId}/prompt-templates`);
  }

  async createPromptTemplate(gameId: string, name: string, type: string, prompt: string) {
    return this.request<PromptTemplate>(`/admin/games/${gameId}/prompt-templates`, {
      method: 'POST',
      body: JSON.stringify({ name, type, prompt }),
    });
  }

  async updatePromptTemplate(gameId: string, templateId: string, name: string, type: string, prompt: string, isActive?: boolean, isDefault?: boolean) {
    return this.request<PromptTemplate>(`/admin/games/${gameId}/prompt-templates/${templateId}`, {
      method: 'PUT',
      body: JSON.stringify({ name, type, prompt, isActive, isDefault }),
    });
  }

  async deletePromptTemplate(gameId: string, templateId: string) {
    return this.request<{ message: string }>(`/admin/games/${gameId}/prompt-templates/${templateId}`, { method: 'DELETE' });
  }

  async getDefaultTemplate(gameId: string, type: string) {
    return this.request<PromptTemplate | null>(`/admin/games/${gameId}/prompt-templates/default/${encodeURIComponent(type)}`);
  }
}

export const api = new APIClient();

