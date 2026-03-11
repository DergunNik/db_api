import { api } from './client';

export interface UserAdminView {
  id: number;
  nick: string;
  is_admin: boolean;
  created_at: string;
  avatar_path: string | null;
  is_banned: boolean;
  ban_reason: string | null;
  ban_end_date: string | null;
}

export interface BanDetails {
  id: number;
  start_date: string;
  end_date: string | null;
  reason: string | null;
  banned_user_nick: string;
  admin_nick: string | null;
}

export async function getAdminUsers(
  page = 1,
  pageSize = 50
): Promise<UserAdminView[]> {
  return api<UserAdminView[]>('/admin/users/', { params: { page, pageSize } });
}

export async function setUserRole(userId: number, is_admin: boolean): Promise<void> {
  await api(`/admin/users/${userId}/role`, {
    method: 'PUT',
    body: JSON.stringify({ is_admin }),
  });
}

export async function deleteUser(userId: number): Promise<void> {
  await api(`/admin/users/${userId}`, { method: 'DELETE' });
}

export async function banFromReport(
  reportId: number,
  reason: string,
  end_date?: string | null
): Promise<{ banId: number }> {
  const res = await api<{ banId: number }>(`/admin/reports/${reportId}/ban`, {
    method: 'POST',
    body: JSON.stringify({ reason, end_date: end_date ?? null }),
  });
  return res;
}

export async function unban(banId: number): Promise<void> {
  await api(`/admin/reports/bans/${banId}`, { method: 'DELETE' });
}

export async function getBans(page = 1): Promise<BanDetails[]> {
  return api<BanDetails[]>('/admin/reports/bans', { params: { page } });
}

export async function getAnalyticsTopUsers(format = 'json'): Promise<unknown> {
  return api(`/admin/analytics/top-users?format=${format}`);
}

export async function getAnalyticsTimeline(format = 'json'): Promise<unknown> {
  return api(`/admin/analytics/timeline?format=${format}`);
}

export async function getAnalyticsCrudStats(format = 'json'): Promise<unknown> {
  return api(`/admin/analytics/crud-stats?format=${format}`);
}

export async function getAnalyticsAnomalies(format = 'json'): Promise<unknown> {
  return api(`/admin/analytics/anomalies?format=${format}`);
}

export async function getAnalyticsHourlyTrends(format = 'json'): Promise<unknown> {
  return api(`/admin/analytics/hourly-trends?format=${format}`);
}

export interface LogEvent {
  _id?: string;
  UserId?: number;
  EventType: string;
  Details: string;
  CreatedAt: string;
}

export async function getLogs(params: {
  userId?: number;
  type?: string;
  windowMinutes?: number;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<{
  filters: unknown;
  pagination: { total: number; page: number; pageSize: number; totalPages: number };
  logs: LogEvent[];
}> {
  const q: Record<string, string | number> = {};
  if (params.userId != null) q.userId = params.userId;
  if (params.type) q.type = params.type;
  if (params.windowMinutes != null) q.windowMinutes = params.windowMinutes;
  if (params.from) q.from = params.from;
  if (params.to) q.to = params.to;
  if (params.page != null) q.page = params.page;
  if (params.pageSize != null) q.pageSize = params.pageSize;
  return api('/admin/logs/', { params: q });
}

export async function getLogsForUser(userId: number, page = 1, pageSize = 50): Promise<{
  userId: number;
  total: number;
  logs: LogEvent[];
}> {
  return api(`/admin/logs/user/${userId}`, { params: { page, pageSize } });
}

export async function getLogErrors(page = 1, pageSize = 50): Promise<{
  total: number;
  logs: LogEvent[];
}> {
  return api('/admin/logs/errors', { params: { page, pageSize } });
}
