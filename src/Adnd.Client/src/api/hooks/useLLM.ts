import { useState, useCallback, useEffect } from 'react';
import { api } from '../client';
import type { LLMPreset, LLMInteractionLog, PresetUsageSummary, GameProviderUsageSummary } from '../../types';

export function useLLMPresets() {
  const [presets, setPresets] = useState<LLMPreset[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchPresets = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getLLMPresets();
      setPresets(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchPresets();
  }, [fetchPresets]);

  const createPreset = async (request: { name: string; providerType: string; baseModel: string; temperature: number; maxTokens: number; topP: number }) => {
    const preset = await api.createLLMPreset(request);
    setPresets(prev => [...prev, preset]);
    return preset;
  };

  const updatePreset = async (presetId: string, request: { name?: string; temperature?: number; maxTokens?: number; topP?: number }) => {
    const preset = await api.updateLLMPreset(presetId, request);
    setPresets(prev => prev.map(p => p.id === presetId ? preset : p));
    return preset;
  };

  const deletePreset = async (presetId: string) => {
    await api.deleteLLMPreset(presetId);
    setPresets(prev => prev.filter(p => p.id !== presetId));
  };

  const testConnection = async (presetId: string) => {
    return api.testLLMPreset(presetId);
  };

  const setDefault = async (presetId: string) => {
    await api.setDefaultPreset(presetId);
    await fetchPresets();
  };

  return {
    presets,
    isLoading,
    error,
    refetch: fetchPresets,
    createPreset,
    updatePreset,
    deletePreset,
    testConnection,
    setDefault,
  };
}

export function useProviderModels() {
  const [models, setModels] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchModels = useCallback(async (providerType: string, endpointUrl?: string, apiKey?: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getProviderModels(providerType, endpointUrl, apiKey);
      setModels(data);
    } catch (e: any) {
      setError(e.message);
      setModels([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  return { models, isLoading, error, fetchModels };
}

export function useUserLLMUsage() {
  const [usage, setUsage] = useState<PresetUsageSummary[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchUsage = useCallback(async (from?: string, to?: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getPresetUsage(from, to);
      setUsage(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    usage,
    isLoading,
    error,
    refetch: fetchUsage,
  };
}

export function useLLMInteractions(gameId?: string) {
  const [logs, setLogs] = useState<LLMInteractionLog[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchLogs = useCallback(async (params?: {
    presetId?: string;
    providerType?: string;
    from?: string;
    to?: string;
    limit?: number;
  }) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getLLMInteractions(params);
      setLogs(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const fetchGameLogs = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getLLMInteractions({ gameId, limit: 200 });
      setLogs(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  const deleteLog = async (logId: string) => {
    await api.deleteLLMInteraction(logId);
    setLogs(prev => prev.filter(l => l.id !== logId));
  };

  return {
    logs,
    isLoading,
    error,
    refetch: gameId ? fetchGameLogs : fetchLogs,
    fetchLogs,
    deleteLog,
  };
}

export function useGameProviderUsage(gameId: string | undefined) {
  const [usage, setUsage] = useState<GameProviderUsageSummary[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchUsage = useCallback(async (from?: string, to?: string) => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getGameProviderUsage(gameId, from, to);
      setUsage(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchUsage();
  }, [fetchUsage]);

  return {
    usage,
    isLoading,
    error,
    refetch: fetchUsage,
  };
}
