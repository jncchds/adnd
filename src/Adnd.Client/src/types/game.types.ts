export enum MessageType {
  // === In-game messages (influence narrative) ===
  InGamePublic = 0,
  InGameWhisper = 1,

  // === OOC messages (never influence narrative) ===
  OOCPublic = 2,
  OOCWhisper = 3,

  // === System / meta messages ===
  Action = 4,
  Dice = 5,
  System = 6,
  GM = 7,
  AgentCall = 8,
  AgentResponse = 9,
}

export enum GameStatus {
  Draft = 0,
  Active = 1,
  Archived = 2,
  Finished = 3,
}

export enum GMStatus {
  Idle = 0,
  Running = 1,
  Paused = 2,
}

export enum PlayerRole {
  Creator = 0,
  Player = 1,
  Spectator = 2,
  Observer = 3,
}

export enum PlayerStatus {
  Active = 0,
  Disconnected = 1,
  Left = 2,
}


export interface GameListItem {
  id: string;
  creatorId: string;
  creatorName: string;
  name: string;
  systemId: string;
  systemVersion?: string;
  status: string;
  gmStatus: string; // 'idle' | 'running' | 'paused'
  createdAt: string;
  inviteCode?: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language?: string;
}

export interface GameDetail {
  id: string;
  creatorId: string;
  creatorName: string;
  name: string;
  systemId: string;
  systemVersion?: string;
  status: string;
  gmStatus: string; // 'idle' | 'running' | 'paused'
  createdAt: string;
  inviteCode?: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language?: string;
}

export interface InviteResponse {
  inviteCode: string;
  inviteUrl: string;
}

export interface GMStatusResponse {
  gameId: string;
  status: string; // 'idle' | 'running' | 'paused'
  lastAction?: string;
  lastActionAt?: string;
}

export interface SwayResponse {
  gameId: string;
  callId: string;
  status: number;
  createdAt: string;
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

export type JsonElement = any;
export type CharacterDetail = any;
