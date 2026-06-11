import { useState, useEffect, useCallback } from 'react';
import { gamesGetGames, gamesCreateGame, gamesDeleteGame, gamesJoinGame, gamesJoinByCode, gamesLeaveGame, gamesGenerateInvite, gamesStartGame, gamesArchiveGame } from '../../api/games/gameApi';
import type { GameListItem } from '../../types';

export function useGames() {
  const [games, setGames] = useState<GameListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchGames = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await gamesGetGames();
      setGames(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchGames();
  }, [fetchGames]);

  const createGame = async (name: string, systemId = 'dnd5e', systemVersion?: string, customSystemJson?: string, llmPresetId?: string, plotSeed?: string, gameParameters?: string, language = 'English') => {
    const game = await gamesCreateGame(name, systemId, systemVersion, customSystemJson, llmPresetId, plotSeed, gameParameters, language);
    setGames(prev => [...prev, game]);
    return game;
  };

  const deleteGame = async (id: string) => {
    await gamesDeleteGame(id);
    setGames(prev => prev.filter(g => g.id !== id));
  };

  const joinGame = async (id: string) => {
    await gamesJoinGame(id);
    await fetchGames();
  };

  const joinByCode = async (code: string) => {
    const result = await gamesJoinByCode(code);
    await fetchGames();
    return result;
  };

  const leaveGame = async (id: string) => {
    await gamesLeaveGame(id);
    await fetchGames();
  };

  const generateInvite = async (id: string) => {
    return gamesGenerateInvite(id);
  };

  const startGame = async (id: string) => {
    await gamesStartGame(id);
    await fetchGames();
  };

  const archiveGame = async (id: string) => {
    await gamesArchiveGame(id);
    await fetchGames();
  };

  return {
    games, isLoading, error, refetch: fetchGames,
    createGame, deleteGame, joinGame, joinByCode, leaveGame,
    generateInvite, startGame, archiveGame,
  };
}
