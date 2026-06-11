import type { CreateGameTemplateRequest, UpdateGameTemplateRequest } from '../../types/template.types';
import { api } from '../client';

export async function templatesGetTemplates() {
  return api.getGameTemplates();
}

export async function templatesGetTemplate(id: string) {
  return api.getGameTemplate(id);
}

export async function templatesCreateTemplate(request: CreateGameTemplateRequest) {
  return api.createGameTemplate(request);
}

export async function templatesUpdateTemplate(id: string, request: UpdateGameTemplateRequest) {
  return api.updateGameTemplate(id, request);
}

export async function templatesDeleteTemplate(id: string) {
  return api.deleteGameTemplate(id);
}
