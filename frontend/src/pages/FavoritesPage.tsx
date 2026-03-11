import { useCallback, useEffect, useState } from 'react';
import {
  getFavorites,
  likePost,
  unlikePost,
  removeFavorite,
} from '../api/posts';
import type { PostDetails } from '../api/posts';
import { PostCard } from '../components/PostCard';
import { ReportModal } from '../components/ReportModal';
import { useAuth } from '../context/AuthContext';

export function FavoritesPage() {
  const { profile } = useAuth();
  const [posts, setPosts] = useState<PostDetails[]>([]);
  const [loading, setLoading] = useState(true);
  const [liked, setLiked] = useState<Set<number>>(new Set());
  const [reportTarget, setReportTarget] = useState<{ id: number; nick: string } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getFavorites(1, 50);
      setPosts(data);
    } catch {
      setPosts([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const handleLike = async (postId: number) => {
    await likePost(postId);
    setLiked((s) => new Set(s).add(postId));
    setPosts((prev) =>
      prev.map((p) => (p.id === postId ? { ...p, likes_count: p.likes_count + 1 } : p))
    );
  };

  const handleUnlike = async (postId: number) => {
    await unlikePost(postId);
    setLiked((s) => {
      const n = new Set(s);
      n.delete(postId);
      return n;
    });
    setPosts((prev) =>
      prev.map((p) =>
        p.id === postId ? { ...p, likes_count: Math.max(0, p.likes_count - 1) } : p
      )
    );
  };

  const handleUnfavorite = async (postId: number) => {
    await removeFavorite(postId);
    setPosts((prev) => prev.filter((p) => p.id !== postId));
  };

  const handleReport = (userId: number) => {
    const post = posts.find((p) => p.author_id === userId);
    if (post) setReportTarget({ id: userId, nick: post.author_nick });
  };

  if (!profile) {
    return (
      <div style={{ textAlign: 'center', padding: '2rem' }}>
        Войдите, чтобы видеть избранное
      </div>
    );
  }

  return (
    <>
      <h2 style={{ marginBottom: '1rem' }}>Избранное</h2>
      {loading ? (
        <div style={{ textAlign: 'center', padding: '2rem' }}>Загрузка…</div>
      ) : posts.length === 0 ? (
        <p style={{ color: 'var(--text-dim)', textAlign: 'center' }}>
          Нет избранных постов
        </p>
      ) : (
        posts.map((post) => (
          <PostCard
            key={post.id}
            post={post}
            onLike={handleLike}
            onUnlike={handleUnlike}
            onFavorite={() => {}}
            onUnfavorite={handleUnfavorite}
            isLiked={liked.has(post.id)}
            isFavorited={true}
            onReport={handleReport}
          />
        ))
      )}
      {reportTarget && (
        <ReportModal
          targetUserId={reportTarget.id}
          targetNick={reportTarget.nick}
          onClose={() => setReportTarget(null)}
          onSuccess={() => setReportTarget(null)}
        />
      )}
    </>
  );
}
