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

    // If unauthorized, try to refresh token
    if (response.status === 401 && this._refreshToken) {
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

  // ==================== Games ====================
  async getGames() {
    return this.request<GameListItem[]>('/games');
  }

  async getGame(id: string) {
    return this.request<GameDetail>('/games/' + id);
  }

  async createGame(name: string, systemId = 'dnd5e', systemVersion?: string, customSystemJson?: string) {
    return this.request<GameDetail>('/games', {
      method: 'POST',
      body: JSON.stringify({ name, systemId, systemVersion, customSystemJson }),
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

  async getPlotThreads(gameId: string) {
    return this.request<PlotThreadListItem[]>(`/admin/games/${gameId}/plot-threads`);
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
}

export const api = new APIClient();

// ==================== Types ====================
export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface GameListItem {
  id: string;
  creatorId: string;
  creatorName: string;
  name: string;
  systemId: string;
  systemVersion?: string;
  status: string;
  createdAt: string;
  inviteCode?: string;
}

export interface GameDetail {
  id: string;
  creatorId: string;
  creatorName: string;
  name: string;
  systemId: string;
  systemVersion?: string;
  status: string;
  createdAt: string;
  inviteCode?: string;
}

export interface InviteResponse {
  inviteCode: string;
  inviteUrl: string;
}

export interface GameSessionListItem {
  id: string;
  title: string;
  description?: string;
  startedAt: string;
  endedAt?: string;
  messageCount: number;
}

export interface GameSessionDetail {
  id: string;
  title: string;
  description?: string;
  startedAt: string;
  endedAt?: string;
}

export interface PlayerListItem {
  id: string;
  characterName: string;
  role: string;
  status: string;
  joinedAt: string;
  character?: CharacterDetail;
  userName?: string;
  userEmail?: string;
}

export interface NPCListItem {
  id: string;
  name: string;
  description?: string;
  attributes: JsonElement;
  skills: JsonElement;
  inventory: JsonElement;
  spells: JsonElement;
  plotThreadId?: string;
  createdAt: string;
}

export interface NPCUpdateRequest {
  name?: string;
  description?: string;
  attributes?: JsonElement;
  skills?: JsonElement;
  inventory?: JsonElement;
  spells?: JsonElement;
  plotThreadId?: string;
}

export interface PlotThreadListItem {
  id: string;
  title: string;
  description: string;
  status: string;
  keyEventMessageIds: string[];
  createdAt: string;
  updatedAt?: string;
}

export interface PlotThreadUpdateRequest {
  title?: string;
  description?: string;
  status?: string;
  keyEventMessageIds?: string[];
}

export interface CharacterListItem {
  id: string;
  name: string;
  class: string;
  level: number;
  proficiencyBonus?: number;
  currentHP: number;
  maxHP: number;
  systemId?: string;
  attributes: JsonElement;
  skills: JsonElement;
  inventory: JsonElement;
  spells: JsonElement;
  conditions: JsonElement;
  updatedAt: string;
  playerName: string;
}

export interface CharacterDetail {
  id: string;
  name: string;
  class: string;
  level: number;
  proficiencyBonus?: number;
  currentHP: number;
  maxHP: number;
  systemId?: string;
  attributes: JsonElement;
  skills: JsonElement;
  inventory: JsonElement;
  spells: JsonElement;
  conditions: JsonElement;
  updatedAt: string;
  playerName: string;
}

export interface CharacterUpdateRequest {
  name?: string;
  class?: string;
  level?: number;
  currentHP?: number;
  maxHP?: number;
  attributes?: JsonElement;
  skills?: JsonElement;
  inventory?: JsonElement;
  spells?: JsonElement;
  conditions?: JsonElement;
}

export interface ProviderStatus {
  providerId: string;
  model: string;
  isAvailable: boolean;
  errorMessage?: string;
  checkedAt: string;
}

export interface ConsistencyReport {
  gameId: string;
  messagesAnalyzed: number;
  checkedAt: string;
  warnings: string[];
  findings: string[];
}

// ==================== Whisper Types ====================

export interface WhisperItem {
  id: string;
  fromPlayerId: string;
  fromCharacter: string;
  fromRole: string;
  content: string;
  type: number;
  targets: string;
  createdAt: string;
}

// ==================== Agent Framework Types ====================

export interface AgentCallItem {
  id: string;
  fromAgent: number;
  toAgent: number;
  action: number;
  status: number;
  output?: string;
  outputMessage?: string;
  durationMs: number;
  error?: string;
  createdAt: string;
  completedAt?: string;
}

// ==================== LLM Preset Types ====================

export interface LLMPreset {
  id: string;
  name: string;
  providerType: string;
  baseUrlModel: string;
  endpointUrl?: string;
  hasApiKey: boolean;
  temperature: number;
  maxTokens: number;
  topP: number;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface LLMPresetDetail {
  id: string;
  name: string;
  providerType: string;
  baseUrlModel: string;
  endpointUrl?: string;
  hasApiKey: boolean;
  temperature: number;
  maxTokens: number;
  topP: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
  stream: boolean;
  embeddingModel?: string;
  embeddingEndpointUrl?: string;
  isDefault: boolean;
  isActive: boolean;
  extraParams?: JsonElement;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateLLMPresetRequest {
  name: string;
  providerType: string;
  baseUrlModel: string;
  endpointUrl?: string;
  apiKey?: string;
  temperature?: number;
  maxTokens?: number;
  topP?: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
  stream?: boolean;
  embeddingModel?: string;
  embeddingEndpointUrl?: string;
  isActive?: boolean;
  isDefault?: boolean;
}

export interface UpdateLLMPresetRequest {
  name?: string;
  providerType?: string;
  baseUrlModel?: string;
  endpointUrl?: string;
  apiKey?: string;
  temperature?: number;
  maxTokens?: number;
  topP?: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
  stream?: boolean;
  embeddingModel?: string;
  embeddingEndpointUrl?: string;
  isActive?: boolean;
  isDefault?: boolean;
}

export interface TestConnectionResponse {
  success: boolean;
  message?: string;
}

// ==================== LLM Interaction Log Types ====================

export interface LLMInteractionLog {
  id: string;
  presetId?: string;
  presetName?: string;
  providerType: string;
  model: string;
  promptTokens?: number;
  completionTokens?: number;
  totalTokens?: number;
  durationMs: number;
  success: boolean;
  error?: string;
  systemPrompt?: string;
  userPrompt?: string;
  response?: string;
  origin: string;
  originGameId?: string;
  originSessionId?: string;
  originAgent?: string;
  originAction?: string;
  startedAt: string;
  completedAt: string;
}

export interface LLMInteractionLogDetail extends LLMInteractionLog {
  requestJson?: string;
  responseJson?: string;
}

export interface PresetUsageSummary {
  presetId: string;
  presetName: string;
  providerType: string;
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  totalTokens: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  avgDurationMs: number;
  lastUsed: string;
}

export type JsonElement = any;
