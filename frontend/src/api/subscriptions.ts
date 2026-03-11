import { api } from './client';

export async function subscribe(targetUserId: number): Promise<void> {
  await api(`/subscriptions/${targetUserId}`, { method: 'POST' });
}

export async function unsubscribe(targetUserId: number): Promise<void> {
  await api(`/subscriptions/${targetUserId}`, { method: 'DELETE' });
}

export async function getSubscriptionStatus(
  targetUserId: number
): Promise<{ isSubscribed: boolean }> {
  return api<{ isSubscribed: boolean }>(`/subscriptions/${targetUserId}/status`);
}

export async function getSubscriptionCounts(): Promise<{
  followers_count: number;
  following_count: number;
}> {
  return api<{ followers_count: number; following_count: number }>('/subscriptions/counts');
}
