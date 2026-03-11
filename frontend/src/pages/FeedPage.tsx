import { useCallback, useState } from 'react';
import {
  getFeed,
  createPost,
  likePost,
  unlikePost,
  addFavorite,
  removeFavorite,
} from '../api/posts';
import type { PostDetails } from '../api/posts';
import { PostCard } from '../components/PostCard';
import { PostComposer } from '../components/PostComposer';
import { ReportModal } from '../components/ReportModal';
import { useAuth } from '../context/AuthContext';
import { useEffect } from 'react';

export function FeedPage() {
  const { profile } = useAuth();
  const [posts, setPosts] = useState<PostDetails[]>([]);
  const [loading, setLoading] = useState(true);
  const [page] = useState(1);
  const [liked, setLiked] = useState<Set<number>>(new Set());
  const [favorited, setFavorited] = useState<Set<number>>(new Set());
  const [reportTarget, setReportTarget] = useState<{ id: number; nick: string } | null>(null);

  const loadFeed = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getFeed(page, 20);
      setPosts(data);
    } catch {
      setPosts([]);
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    loadFeed();
  }, [loadFeed]);

  const handleCreatePost = async (text: string) => {
    await createPost(text);
    loadFeed();
  };

  const handleLike = async (postId: number) => {
    await likePost(postId);
    setLiked((s) => new Set(s).add(postId));
    setPosts((prev) =>
      prev.map((p) =>
        p.id === postId ? { ...p, likes_count: p.likes_count + 1 } : p
      )
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

  const handleFavorite = async (postId: number) => {
    await addFavorite(postId);
    setFavorited((s) => new Set(s).add(postId));
  };

  const handleUnfavorite = async (postId: number) => {
    await removeFavorite(postId);
    setFavorited((s) => {
      const n = new Set(s);
      n.delete(postId);
      return n;
    });
  };

  const handleReport = (userId: number) => {
    const post = posts.find((p) => p.author_id === userId);
    if (post) setReportTarget({ id: userId, nick: post.author_nick });
  };

  if (!profile) {
    return (
      <div style={{ textAlign: 'center', padding: '2rem' }}>
        Войдите, чтобы видеть ленту
      </div>
    );
  }

  return (
    <>
      <PostComposer onSubmit={handleCreatePost} />
      {loading ? (
        <div style={{ textAlign: 'center', padding: '2rem' }}>Загрузка…</div>
      ) : posts.length === 0 ? (
        <p style={{ color: 'var(--text-dim)', textAlign: 'center' }}>
          Лента пуста. Подпишитесь на пользователей.
        </p>
      ) : (
        posts.map((post) => (
          <PostCard
            key={post.id}
            post={post}
            onLike={handleLike}
            onUnlike={handleUnlike}
            onFavorite={handleFavorite}
            onUnfavorite={handleUnfavorite}
            isLiked={liked.has(post.id)}
            isFavorited={favorited.has(post.id)}
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
