import { useState, useEffect, useCallback } from 'react';
import { adminGetCharacters as adminGetChars, adminGetCharacter as adminGetChar, adminUpdateCharacter as adminUpdateChar, adminCheckConsistency } from '../../api/admin/adminApi';
import type { CharacterDetail } from '../../types/game.types';

export function useCharacters(gameId: string | undefined) {
  const [characters, setCharacters] = useState<{ id: string; name: string; characterClass: string; level: number }[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCharacters = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await adminGetChars(gameId);
      setCharacters(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchCharacters();
  }, [fetchCharacters]);

  const updateCharacter = async (characterId: string, updates: Record<string, any>) => {
    await adminUpdateChar(characterId, updates);
    await fetchCharacters();
  };

  return { characters, isLoading, error, refetch: fetchCharacters, updateCharacter };
}

export function useCharacter(characterId: string | undefined) {
  const [character, setCharacter] = useState<CharacterDetail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCharacter = useCallback(async () => {
    if (!characterId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await adminGetChar(characterId);
      setCharacter(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [characterId]);

  useEffect(() => {
    fetchCharacter();
  }, [fetchCharacter]);

  const updateCharacter = async (updates: Record<string, any>) => {
    await adminUpdateChar(characterId!, updates);
    await fetchCharacter();
  };

  return { character, isLoading, error, refetch: fetchCharacter, updateCharacter };
}

export function useConsistency(gameId: string | undefined) {
  const [report, setReport] = useState<{ inconsistencies: string[] } | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const check = useCallback(async (messageCount = 50) => {
    if (!gameId) return;
    setIsLoading(true);
    try {
      const data = await adminCheckConsistency(gameId, messageCount);
      setReport(data as any);
    } catch (e) {
      console.error('Consistency check failed', e);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  return { report, isLoading, check };
}
