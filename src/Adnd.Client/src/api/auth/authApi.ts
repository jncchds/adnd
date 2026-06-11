import { api } from '../client';

export async function authRegister(email: string, password: string, displayName: string) {
  return api.register(email, password, displayName);
}

export async function authLogin(email: string, password: string) {
  return api.login(email, password);
}

export async function authLogout(refreshToken: string | null) {
  if (refreshToken) {
    await api.logout().catch(() => {});
  }
}

export async function authGetMe() {
  return api.getMe();
}

export async function authChangePassword(currentPassword: string, newPassword: string) {
  return api.changePassword(currentPassword, newPassword);
}

export async function authUpdateDisplayName(displayName: string) {
  return api.updateDisplayName(displayName);
}
