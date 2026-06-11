import type { CreateLLMPresetRequest, UpdateLLMPresetRequest } from '../../types/llm.types';
import { api } from '../client';

// Providers
export async function llmGetProviders() {
  return api.getLLMProviders();
}

export async function llmGetProviderModels(providerType: string, endpointUrl?: string, apiKey?: string) {
  return api.getProviderModels(providerType, endpointUrl, apiKey);
}

// Presets
export async function llmGetPresets() {
  return api.getLLMPresets();
}

export async function llmGetPreset(presetId: string) {
  return api.getLLMPreset(presetId);
}

export async function llmCreatePreset(request: CreateLLMPresetRequest) {
  return api.createLLMPreset(request);
}

export async function llmUpdatePreset(presetId: string, request: UpdateLLMPresetRequest) {
  return api.updateLLMPreset(presetId, request);
}

export async function llmDeletePreset(presetId: string) {
  return api.deleteLLMPreset(presetId);
}

export async function llmTestPreset(presetId: string) {
  return api.testLLMPreset(presetId);
}

export async function llmSetDefaultPreset(presetId: string) {
  return api.setDefaultPreset(presetId);
}

// Interaction Logs
export async function llmGetInteractions(params?: { presetId?: string; gameId?: string; providerType?: string; from?: string; to?: string; limit?: number }) {
  const searchParams = new URLSearchParams();
  if (params?.presetId) searchParams.set('presetId', params.presetId);
  if (params?.gameId) searchParams.set('gameId', params.gameId);
  if (params?.providerType) searchParams.set('providerType', params.providerType);
  if (params?.from) searchParams.set('from', params.from);
  if (params?.to) searchParams.set('to', params.to);
  if (params?.limit) searchParams.set('limit', String(params.limit));
  return api.getLLMInteractions({ presetId: params?.presetId, gameId: params?.gameId, providerType: params?.providerType, from: params?.from, to: params?.to, limit: params?.limit });
}

export async function llmGetInteraction(logId: string) {
  return api.getLLMInteraction(logId);
}

export async function llmDeleteInteraction(logId: string) {
  return api.deleteLLMInteraction(logId);
}

export async function llmGetPresetUsage(from?: string, to?: string) {
  const searchParams = new URLSearchParams();
  if (from) searchParams.set('from', from);
  if (to) searchParams.set('to', to);
  return api.getPresetUsage(from, to);
}

export async function llmCleanupOldLogs(before: string) {
  return api.cleanupOldLogs(before);
}

export async function llmGetGameProviderUsage(gameId: string, from?: string, to?: string) {
  const searchParams = new URLSearchParams();
  if (from) searchParams.set('from', from);
  if (to) searchParams.set('to', to);
  return api.getGameProviderUsage(gameId, from, to);
}
