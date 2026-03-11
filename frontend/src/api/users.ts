import { api } from './client';

export interface UserProfile {
  id: number;
  nick: string;
  is_admin: boolean;
  created_at: string;
  avatar_path: string | null;
  followers_count: number;
  following_count: number;
  posts_count: number;
}

export interface UserSearchResult {
  id: number;
  nick: string;
  avatar_path: string | null;
}

export interface UserSummary {
  id: number;
  nick: string;
  avatar_path: string | null;
  relation_date: string;
}

export async function getProfile(): Promise<UserProfile> {
  return api<UserProfile>('/users/profile');
}

export async function searchUsers(query: string, limit = 20): Promise<UserSearchResult[]> {
  return api<UserSearchResult[]>('/users/search', { params: { query, limit } });
}

export async function updateProfile(nick?: string, avatar_id?: number): Promise<void> {
  await api('/users/profile', {
    method: 'PUT',
    body: JSON.stringify({ nick: nick ?? null, avatar_id: avatar_id ?? null }),
  });
}

export async function getFollowers(
  userId: number,
  page = 1,
  pageSize = 20
): Promise<UserSummary[]> {
  return api<UserSummary[]>(`/users/${userId}/followers`, { params: { page, pageSize } });
}

export async function getFollowing(
  userId: number,
  page = 1,
  pageSize = 20
): Promise<UserSummary[]> {
  return api<UserSummary[]>(`/users/${userId}/following`, { params: { page, pageSize } });
}
