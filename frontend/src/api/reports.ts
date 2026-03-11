import { api } from './client';

export interface ReportDetails {
  id: number;
  author_id: number;
  comment: string | null;
  is_reviewed: boolean;
  author_nick: string | null;
  target_user_nick: string | null;
  post_text: string | null;
}

export async function createReport(
  targetUserId: number,
  comment: string
): Promise<{ reportId: number }> {
  const res = await api<{ reportId: number }>(`/reports/user/${targetUserId}`, {
    method: 'POST',
    body: JSON.stringify({ comment }),
  });
  return res;
}

export async function getReport(reportId: number): Promise<ReportDetails> {
  return api<ReportDetails>(`/reports/${reportId}`);
}
