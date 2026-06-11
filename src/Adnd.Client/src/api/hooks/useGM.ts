import { useState, useCallback } from 'react';
import { api } from '../client';
import type { GMToolDefinition, SpellEntry, SpellSlotInfo, SpellUpdateRequest } from '../../types';

export function useGMTools(gameId: string | undefined) {
  const [tools, setTools] = useState<GMToolDefinition[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchTools = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getGMTools(gameId);
      setTools(data.tools);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  const executeTool = useCallback(async (toolName: string, args: Record<string, any>, sessionId?: string) => {
    if (!gameId) return null;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.executeGMTool(gameId, sessionId || '', {
        toolName,
        arguments: JSON.stringify(args),
      });
      return data;
    } catch (e: any) {
      setError(e.message);
      return null;
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  return {
    tools,
    isLoading,
    error,
    refetch: fetchTools,
    executeTool,
  };
}

export function useSpells(characterId: string | undefined) {
  const [spells, setSpells] = useState<SpellEntry[]>([]);
  const [spellSlots, setSpellSlots] = useState<SpellSlotInfo[]>([]);
  const [characterName, setCharacterName] = useState('');
  const [characterClass, setCharacterClass] = useState('');
  const [characterLevel, setCharacterLevel] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchSpells = useCallback(async () => {
    if (!characterId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getCharacterSpells(characterId);
      setSpells(data.spells);
      setSpellSlots(data.spellSlots);
      setCharacterName(data.characterName);
      setCharacterClass(data.characterClass);
      setCharacterLevel(data.characterLevel);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [characterId]);

  const updateSpells = useCallback(async (spellUpdates: SpellUpdateRequest[]) => {
    if (!characterId) return false;
    try {
      await api.updateCharacterSpells(characterId, spellUpdates);
      await fetchSpells();
      return true;
    } catch {
      return false;
    }
  }, [characterId, fetchSpells]);

  const addSpell = useCallback(async (spell: SpellUpdateRequest) => {
    if (!characterId || !spell.name) return false;
    try {
      await api.updateSingleSpell(characterId, spell.name, spell);
      await fetchSpells();
      return true;
    } catch {
      return false;
    }
  }, [characterId, fetchSpells]);

  const removeSpell = useCallback(async (spellName: string) => {
    if (!characterId) return false;
    try {
      await api.removeSpell(characterId, spellName);
      await fetchSpells();
      return true;
    } catch {
      return false;
    }
  }, [characterId]);

  return {
    spells,
    spellSlots,
    characterName,
    characterClass,
    characterLevel,
    isLoading,
    error,
    refetch: fetchSpells,
    updateSpells,
    addSpell,
    removeSpell,
  };
}
