import { api } from './client';

export interface PostDetails {
  id: number;
  text: string | null;
  answer_to_id: number | null;
  created_at: string;
  author_nick: string;
  author_id: number;
  author_avatar: string | null;
  post_media: string | null;
  likes_count: number;
  replies_count: number;
}

export interface PostSummary {
  id: number;
  text: string | null;
  created_at: string;
  author_nick: string;
  author_id?: number;
  post_media: string | null;
  likes_count: number;
}

export interface PostWithReplies {
  post: PostDetails;
  replies: PostSummary[];
}

export async function createPost(
  text: string,
  answer_to_id?: number,
  media_id?: number
): Promise<PostDetails> {
  return api<PostDetails>('/posts/', {
    method: 'POST',
    body: JSON.stringify({ text, answer_to_id: answer_to_id ?? null, media_id: media_id ?? null }),
  });
}

export async function getPost(postId: number): Promise<PostWithReplies> {
  return api<PostWithReplies>(`/posts/${postId}`);
}

export async function getFeed(page = 1, pageSize = 20): Promise<PostDetails[]> {
  return api<PostDetails[]>('/posts/feed', { params: { page, pageSize } });
}

export async function getUserPosts(
  userId: number,
  page = 1,
  pageSize = 20
): Promise<PostDetails[]> {
  return api<PostDetails[]>(`/posts/user/${userId}`, { params: { page, pageSize } });
}

export async function updatePost(
  postId: number,
  text: string,
  media_id?: number
): Promise<void> {
  await api(`/posts/${postId}`, {
    method: 'PUT',
    body: JSON.stringify({ text, media_id: media_id ?? null }),
  });
}

export async function deletePost(postId: number): Promise<void> {
  await api(`/posts/${postId}`, { method: 'DELETE' });
}

export async function likePost(postId: number): Promise<void> {
  await api(`/posts/${postId}/like`, { method: 'POST' });
}

export async function unlikePost(postId: number): Promise<void> {
  await api(`/posts/${postId}/like`, { method: 'DELETE' });
}

export async function addFavorite(postId: number): Promise<void> {
  await api(`/posts/${postId}/favorite`, { method: 'POST' });
}

export async function removeFavorite(postId: number): Promise<void> {
  await api(`/posts/${postId}/favorite`, { method: 'DELETE' });
}

export async function getFavorites(page = 1, pageSize = 20): Promise<PostDetails[]> {
  return api<PostDetails[]>('/posts/favorites', { params: { page, pageSize } });
}
