import { useState, useEffect, useCallback } from 'react';

/**
 * Generic entity hook that replaces duplicated useNPCs, usePlotThreads,
 * useCharacters, useSessions, useLLMPresets patterns.
 *
 * @param fetchFn - Function to fetch the entity list
 * @param deps - Dependencies that trigger re-fetch
 * @param createFn - Optional function to create a new entity
 * @param deleteFn - Optional function to delete an entity
 * @param updateFn - Optional function to update an entity
 */
export function useEntity<T, C = T, U = T, D = void>(
  fetchFn: () => Promise<T[]>,
  deps: any[] = [],
  createFn?: (data: Partial<T>) => Promise<C>,
  deleteFn?: (id: string) => Promise<D>,
  updateFn?: (id: string, data: Partial<T>) => Promise<U>
) {
  const [items, setItems] = useState<T[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetch = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await fetchFn();
      setItems(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, deps);

  useEffect(() => {
    fetch();
  }, [fetch]);

  const create = async (data: Partial<T>) => {
    if (!createFn) return null;
    const item = await createFn(data) as unknown as T;
    setItems(prev => [...prev, item]);
    return item as unknown as C;
  };

  const remove = async (id: string) => {
    if (!deleteFn) return;
    await deleteFn(id);
    setItems(prev => prev.filter(i => (i as any).id !== id));
  };

  const update = async (id: string, data: Partial<T>) => {
    if (!updateFn) return null;
    const item = await updateFn(id, data) as unknown as T;
    setItems(prev => prev.map(i => (i as any).id === id ? item : i));
    return item as unknown as U;
  };

  return {
    items,
    isLoading,
    error,
    refetch: fetch,
    create,
    remove,
    update,
  };
}
