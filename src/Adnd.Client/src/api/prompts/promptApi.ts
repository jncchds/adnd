import { api } from '../client';

export async function promptsGetTemplates(gameId: string) {
  return api.getPromptTemplates(gameId);
}

export async function promptsCreateTemplate(gameId: string, name: string, type: string, prompt: string) {
  return api.createPromptTemplate(gameId, name, type, prompt);
}

export async function promptsUpdateTemplate(gameId: string, templateId: string, name: string, type: string, prompt: string, isActive?: boolean, isDefault?: boolean) {
  return api.updatePromptTemplate(gameId, templateId, name, type, prompt, isActive, isDefault);
}

export async function promptsDeleteTemplate(gameId: string, templateId: string) {
  return api.deletePromptTemplate(gameId, templateId);
}

export async function promptsGetDefaultTemplate(gameId: string, type: string) {
  return api.getDefaultTemplate(gameId, type);
}
