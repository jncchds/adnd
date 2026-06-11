import { useState, useEffect, useCallback } from 'react';
import { gamesGetNPCs, gamesCreateNPC, gamesUpdateNPC, gamesDeleteNPC } from '../../api/games/gameApi';

export function useNPCs(gameId: string | undefined) {
  const [npcs, setNpcs] = useState<{ id: string; name: string; description?: string }[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchNpcs = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await gamesGetNPCs(gameId);
      setNpcs(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    fetchNpcs();
  }, [fetchNpcs]);

  const createNPC = async (name: string, description?: string) => {
    await gamesCreateNPC(gameId!, name, description);
    await fetchNpcs();
  };

  const updateNPC = async (npcId: string, updates: { name?: string; description?: string }) => {
    await gamesUpdateNPC(npcId, updates);
    await fetchNpcs();
  };

  const deleteNPC = async (npcId: string) => {
    await gamesDeleteNPC(npcId);
    setNpcs(prev => prev.filter(n => n.id !== npcId));
  };

  return { npcs, isLoading, error, refetch: fetchNpcs, createNPC, updateNPC, deleteNPC };
}
