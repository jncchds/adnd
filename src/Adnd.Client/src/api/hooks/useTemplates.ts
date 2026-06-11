import { useState, useCallback, useEffect } from 'react';
import { templatesGetTemplates, templatesCreateTemplate, templatesUpdateTemplate, templatesDeleteTemplate } from '../../api/templates/templateApi';
import type { GameTemplate, CreateGameTemplateRequest, UpdateGameTemplateRequest } from '../../types';

export function useGameTemplates() {
  const [templates, setTemplates] = useState<GameTemplate[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchTemplates = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await templatesGetTemplates();
      setTemplates(data);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTemplates();
  }, [fetchTemplates]);

  const createTemplate = async (request: CreateGameTemplateRequest) => {
    const template = await templatesCreateTemplate(request);
    setTemplates(prev => [...prev, template]);
    return template;
  };

  const updateTemplate = async (id: string, request: UpdateGameTemplateRequest) => {
    const template = await templatesUpdateTemplate(id, request);
    setTemplates(prev => prev.map(t => t.id === id ? template : t));
    return template;
  };

  const deleteTemplate = async (id: string) => {
    await templatesDeleteTemplate(id);
    setTemplates(prev => prev.filter(t => t.id !== id));
  };

  return {
    templates,
    isLoading,
    error,
    refetch: fetchTemplates,
    createTemplate,
    updateTemplate,
    deleteTemplate,
  };
}
