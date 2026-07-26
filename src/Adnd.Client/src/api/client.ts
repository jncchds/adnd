import type {
  AuthResponse, LoginRequest, RegisterRequest, User,
  Game, GameCreateRequest, Player, GameSession, NPC,
  LLMPreset, LLMPresetCreate, LLMPresetUpdate, ProviderStatus, LLMInteractionLog,
  Message, MessagePage,
  AgentCall, ToolCall,
  PlotThread, PlotContext, ConsistencyReport, PlotContinuation,
  GMStatusResponse,
  PromptTemplate, GameTemplate, SessionNote,
  Combat,
} from '../types'

const TOKEN_KEY = 'adnd-token'
const REFRESH_KEY = 'adnd-refresh'

class APIClient {
  private baseUrl = '/api'

  getToken(): string | null { return localStorage.getItem(TOKEN_KEY) }
  getRefreshToken(): string | null { return localStorage.getItem(REFRESH_KEY) }

  setTokens(token: string, refreshToken: string) {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(REFRESH_KEY, refreshToken)
  }

  clearTokens() {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_KEY)
  }

  private async request<T>(path: string, options: RequestInit = {}, retry = true): Promise<T> {
    const token = this.getToken()
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string> ?? {}),
    }
    if (token) headers['Authorization'] = `Bearer ${token}`

    const res = await fetch(`${this.baseUrl}${path}`, { ...options, headers })

    if (res.status === 401 && retry) {
      const refreshed = await this.tryRefresh()
      if (refreshed) return this.request<T>(path, options, false)
      this.clearTokens()
      window.location.href = '/login'
      throw new Error('Session expired')
    }

    if (res.status === 429) throw new Error('Rate limit exceeded. Please slow down.')

    if (!res.ok) {
      const body = await res.text().catch(() => res.statusText)
      throw new Error(body || `HTTP ${res.status}`)
    }

    if (res.status === 204) return undefined as T
    return res.json()
  }

  private async tryRefresh(): Promise<boolean> {
    const refreshToken = this.getRefreshToken()
    if (!refreshToken) return false
    try {
      const res = await fetch(`${this.baseUrl}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!res.ok) return false
      const data: AuthResponse = await res.json()
      this.setTokens(data.token, data.refreshToken)
      return true
    } catch { return false }
  }

  private get<T>(path: string) { return this.request<T>(path) }
  private post<T>(path: string, body?: unknown) {
    return this.request<T>(path, { method: 'POST', body: JSON.stringify(body) })
  }
  private put<T>(path: string, body?: unknown) {
    return this.request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
  }
  private patch<T>(path: string, body?: unknown) {
    return this.request<T>(path, { method: 'PATCH', body: JSON.stringify(body) })
  }
  private delete<T>(path: string) { return this.request<T>(path, { method: 'DELETE' }) }

  // Auth
  auth = {
    register: (data: RegisterRequest) => this.post<AuthResponse>('/auth/register', data),
    login: (data: LoginRequest) => this.post<AuthResponse>('/auth/login', data),
    logout: () => this.post<void>('/auth/logout', { refreshToken: this.getRefreshToken() }),
    me: () => this.get<User>('/auth/me'),
    changePassword: (current: string, newPassword: string) =>
      this.post<void>('/auth/change-password', { currentPassword: current, newPassword }),
    updateDisplayName: (displayName: string) =>
      this.put<void>('/auth/display-name', { displayName }),
  }

  // Games
  games = {
    list: () => this.get<Game[]>('/games'),
    get: (id: string) => this.get<Game>(`/games/${id}`),
    create: (data: GameCreateRequest) => this.post<Game>('/games', data),
    update: (id: string, data: Partial<GameCreateRequest>) => this.put<Game>(`/games/${id}`, data),
    delete: (id: string) => this.delete<void>(`/games/${id}`),
    start: (id: string) => this.post<void>(`/games/${id}/start`),
    archive: (id: string) => this.post<void>(`/games/${id}/archive`),
    generateInvite: (id: string) => this.post<{ code: string }>(`/games/${id}/invite`),
    joinByCode: (code: string) => this.post<Game>('/games/join', { code }),
    join: (id: string) => this.post<void>(`/games/${id}/join`),
    leave: (id: string) => this.post<void>(`/games/${id}/leave`),
    getPlayers: (id: string) => this.get<Player[]>(`/games/${id}/players`),
    promotePlayer: (id: string, playerId: string, role: string) =>
      this.patch<void>(`/games/${id}/players/${playerId}/role`, { role }),
    getSessions: (id: string) => this.get<GameSession[]>(`/games/${id}/sessions`),
    gmStatus: (id: string) => this.get<GMStatusResponse>(`/gmstatus/${id}`),
    pauseGM: (id: string) => this.post<void>(`/gmstatus/${id}/pause`),
    resumeGM: (id: string) => this.post<void>(`/gmstatus/${id}/resume`),
    sway: (id: string, direction: string, intensity: number, content: string) =>
      this.post<void>(`/sway/${id}`, { direction, intensity, content }),
    getMessages: (sessionId: string, cursor?: string, limit = 30) =>
      this.get<MessagePage>(`/games/sessions/${sessionId}/messages?limit=${limit}${cursor ? `&cursor=${cursor}` : ''}`),
  }

  // NPCs
  npcs = {
    list: (gameId: string) => this.get<NPC[]>(`/npcs?gameId=${gameId}`),
    get: (id: string) => this.get<NPC>(`/npcs/${id}`),
    create: (data: Partial<NPC>) => this.post<NPC>('/npcs', data),
    update: (id: string, data: Partial<NPC>) => this.put<NPC>(`/npcs/${id}`, data),
    delete: (id: string) => this.delete<void>(`/npcs/${id}`),
  }

  // LLM Presets
  llmPresets = {
    list: () => this.get<LLMPreset[]>('/llmpresets'),
    get: (id: string) => this.get<LLMPreset>(`/llmpresets/${id}`),
    create: (data: LLMPresetCreate) => this.post<LLMPreset>('/llmpresets', data),
    update: (id: string, data: LLMPresetUpdate) => this.put<LLMPreset>(`/llmpresets/${id}`, data),
    delete: (id: string) => this.delete<void>(`/llmpresets/${id}`),
    setDefault: (id: string) => this.post<void>(`/llmpresets/${id}/set-default`),
    test: (id: string) => this.post<ProviderStatus>(`/llmpresets/${id}/test`),
    listModels: (id: string) => this.get<string[]>(`/llmpresets/${id}/models`),
  }

  // LLM Logs
  llmLogs = {
    list: (gameId?: string, page = 1, pageSize = 20) =>
      this.get<{ items: LLMInteractionLog[]; total: number }>(
        `/llmlogs?page=${page}&pageSize=${pageSize}${gameId ? `&gameId=${gameId}` : ''}`
      ),
    get: (id: string) => this.get<LLMInteractionLog>(`/llmlogs/${id}`),
    delete: (id: string) => this.delete<void>(`/llmlogs/${id}`),
    deleteByGame: (gameId: string) => this.delete<void>(`/llmlogs/game/${gameId}`),
    usageStats: (gameId?: string) =>
      this.get<Record<string, unknown>>(`/llmlogs/stats${gameId ? `?gameId=${gameId}` : ''}`),
  }

  // Agent calls
  agentCalls = {
    list: (gameId: string) => this.get<AgentCall[]>(`/agentframework?gameId=${gameId}`),
    get: (id: string) => this.get<AgentCall>(`/agentframework/${id}`),
    pending: (gameId: string) => this.get<AgentCall[]>(`/agentframework/pending?gameId=${gameId}`),
  }

  // Tool calls
  toolCalls = {
    pending: (gameId: string) => this.get<ToolCall[]>(`/gmtools/pending?gameId=${gameId}`),
    confirm: (id: string) => this.post<void>(`/gmtools/${id}/confirm`),
    execute: (gameId: string, toolName: string, args: Record<string, unknown>) =>
      this.post<unknown>(`/gmtools/execute`, { gameId, toolName, arguments: args }),
    list: (category?: string) =>
      this.get<unknown[]>(`/gmtools${category ? `?category=${category}` : ''}`),
  }

  // Plot threads
  plots = {
    list: (gameId: string) => this.get<PlotThread[]>(`/plots?gameId=${gameId}`),
    get: (id: string) => this.get<PlotThread>(`/plots/${id}`),
    create: (data: Partial<PlotThread>) => this.post<PlotThread>('/plots', data),
    update: (id: string, data: Partial<PlotThread>) => this.put<PlotThread>(`/plots/${id}`, data),
    delete: (id: string) => this.delete<void>(`/plots/${id}`),
    context: (gameId: string) => this.get<PlotContext>(`/plotweaver/context/${gameId}`),
    consistency: (gameId: string) => this.get<ConsistencyReport>(`/plotweaver/consistency/${gameId}`),
    continuation: (gameId: string) => this.get<PlotContinuation>(`/plotweaver/continuation/${gameId}`),
    review: (gameId: string) => this.post<void>(`/plotweaver/review/${gameId}`),
    sessionSummary: (gameId: string, sessionId: string) =>
      this.post<void>(`/plotweaver/session-summary/${gameId}/${sessionId}`),
  }

  // Combat
  combat = {
    list: (gameId: string) => this.get<Combat[]>(`/combatlog?gameId=${gameId}`),
    get: (id: string) => this.get<Combat>(`/combatlog/${id}`),
  }

  // Characters
  characters = {
    get: (id: string) => this.get<Record<string, unknown>>(`/characters/${id}`),
    create: (data: Record<string, unknown>) => this.post<Record<string, unknown>>('/characters', data),
    update: (id: string, data: Record<string, unknown>) =>
      this.put<Record<string, unknown>>(`/characters/${id}`, data),
    getForPlayer: (gameId: string, playerId: string) =>
      this.get<Record<string, unknown>>(`/characters?gameId=${gameId}&playerId=${playerId}`),
  }

  // Prompt templates
  promptTemplates = {
    list: (gameId?: string) =>
      this.get<PromptTemplate[]>(`/quickwins/prompt-templates${gameId ? `?gameId=${gameId}` : ''}`),
    create: (data: Partial<PromptTemplate>) =>
      this.post<PromptTemplate>('/quickwins/prompt-templates', data),
    update: (id: string, data: Partial<PromptTemplate>) =>
      this.put<PromptTemplate>(`/quickwins/prompt-templates/${id}`, data),
    delete: (id: string) => this.delete<void>(`/quickwins/prompt-templates/${id}`),
    getDefault: (type: string) =>
      this.get<PromptTemplate>(`/quickwins/prompt-templates/default?type=${type}`),
  }

  // Game templates
  gameTemplates = {
    list: () => this.get<GameTemplate[]>('/quickwins/game-templates'),
    create: (data: Partial<GameTemplate>) => this.post<GameTemplate>('/quickwins/game-templates', data),
    update: (id: string, data: Partial<GameTemplate>) =>
      this.put<GameTemplate>(`/quickwins/game-templates/${id}`, data),
    delete: (id: string) => this.delete<void>(`/quickwins/game-templates/${id}`),
  }

  // Session notes
  sessionNotes = {
    list: (gameId: string) => this.get<SessionNote[]>(`/quickwins/session-notes?gameId=${gameId}`),
    create: (data: Partial<SessionNote>) => this.post<SessionNote>('/quickwins/session-notes', data),
    update: (id: string, data: Partial<SessionNote>) =>
      this.put<SessionNote>(`/quickwins/session-notes/${id}`, data),
    delete: (id: string) => this.delete<void>(`/quickwins/session-notes/${id}`),
  }

  // Manual LLM triggers
  triggers = {
    narrate: (gameId: string, prompt: string) =>
      this.post<void>(`/llmtrigger/${gameId}/narrate`, { prompt }),
    suggest: (gameId: string) => this.post<void>(`/llmtrigger/${gameId}/suggest`),
    consistency: (gameId: string) => this.post<ConsistencyReport>(`/llmtrigger/${gameId}/consistency`),
    generateThreads: (gameId: string) => this.post<void>(`/llmtrigger/${gameId}/generate-threads`),
    sessionSummary: (gameId: string) => this.post<string>(`/llmtrigger/${gameId}/session-summary`),
  }

  // Systems
  systems = {
    list: () => this.get<{ id: string; name: string }[]>('/systems'),
    getCustom: (gameId: string) => this.get<unknown>(`/systems/custom/${gameId}`),
    createCustom: (gameId: string, definition: unknown) =>
      this.post<unknown>(`/systems/custom/${gameId}`, definition),
  }

  // Dice
  dice = {
    history: (gameId: string, page = 1) =>
      this.get<{ items: Message[]; total: number }>(`/dicehistory?gameId=${gameId}&page=${page}`),
    myRolls: (gameId: string) => this.get<Message[]>(`/dice/my-rolls?gameId=${gameId}`),
  }

  // Whispers
  whispers = {
    list: (sessionId: string) => this.get<unknown[]>(`/whispers?sessionId=${sessionId}`),
    send: (sessionId: string, content: string, targetPlayerIds: string[]) =>
      this.post<void>('/whispers', { sessionId, content, targetPlayerIds }),
  }

  // Admin
  admin = {
    getGameState: (gameId: string) => this.get<Record<string, unknown>>(`/gamestate/${gameId}`),
    updateGameState: (gameId: string, state: string) =>
      this.put<void>(`/gamestate/${gameId}`, { state }),
    generateRecap: (gameId: string) =>
      this.post<string>(`/llmtrigger/${gameId}/session-summary`),
  }
}

export const api = new APIClient()
