export interface GameTemplate {
  id: string;
  name: string;
  defaultName?: string;
  systemId: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language: string;
  plotSeed?: string;
  gameParameters?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateGameTemplateRequest {
  name: string;
  defaultName?: string;
  systemId: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language: string;
  plotSeed?: string;
  gameParameters?: string;
}

export interface UpdateGameTemplateRequest {
  name: string;
  defaultName?: string;
  systemId: string;
  llmPresetId?: string;
  llmPresetName?: string;
  language: string;
  plotSeed?: string;
  gameParameters?: string;
}

export interface PromptTemplate {
  id: string;
  gameId: string;
  userId: string;
  userName: string;
  name: string;
  type: string;
  prompt: string;
  isActive: boolean;
  isDefault: boolean;
  createdAt: string;
  updatedAt?: string;
}
