import { api } from './client';

export interface ChatSummary {
  id: number;
  chat_with_nick: string;
  chat_with_id: number;
  chat_with_avatar: string | null;
  chat_created_at: string;
  last_message: string | null;
  last_message_time: string | null;
  unread_count: number;
}

export interface MessageDetails {
  id: number;
  content: string;
  created_at: string;
  author_nick: string;
  author_id: number;
  attached_file: string | null;
}

export async function getChats(): Promise<ChatSummary[]> {
  return api<ChatSummary[]>('/chats/');
}

export async function getOrCreateChat(targetUserId: number): Promise<{ chatId: number }> {
  return api<{ chatId: number }>(`/chats/with/${targetUserId}`, { method: 'PUT' });
}

export async function getMessages(chatId: number): Promise<MessageDetails[]> {
  return api<MessageDetails[]>(`/chats/${chatId}/messages`);
}

export async function sendMessage(
  chatId: number,
  content: string,
  media_id?: number
): Promise<MessageDetails> {
  return api<MessageDetails>(`/chats/${chatId}/messages`, {
    method: 'POST',
    body: JSON.stringify({ content, media_id: media_id ?? null }),
  });
}
