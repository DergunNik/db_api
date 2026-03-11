import { api } from './client';

export async function register(nick: string, password: string): Promise<void> {
  await api('/auth/register', {
    method: 'POST',
    body: JSON.stringify({ nick, password }),
  });
}

export async function login(nick: string, password: string): Promise<{ token: string }> {
  const res = await api<{ token: string }>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ nick, password }),
  });
  return res;
}

export async function logout(): Promise<void> {
  await api('/auth/logout', { method: 'POST' });
}
