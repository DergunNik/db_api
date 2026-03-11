import { useCallback, useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  getPost,
  createPost,
  likePost,
  unlikePost,
  addFavorite,
  removeFavorite,
} from '../api/posts';
import type { PostWithReplies } from '../api/posts';
import { PostCard } from '../components/PostCard';
import { PostComposer } from '../components/PostComposer';
import { ReportModal } from '../components/ReportModal';
import { useAuth } from '../context/AuthContext';

export function PostPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { profile } = useAuth();
  const [data, setData] = useState<PostWithReplies | null>(null);
  const [loading, setLoading] = useState(true);
  const [replyMode, setReplyMode] = useState(false);
  const [liked, setLiked] = useState<Set<number>>(new Set());
  const [favorited, setFavorited] = useState<Set<number>>(new Set());
  const [reportTarget, setReportTarget] = useState<{ id: number; nick: string } | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const d = await getPost(Number(id));
      setData(d);
    } catch {
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const handleReply = async (text: string) => {
    if (!data) return;
    await createPost(text, data.post.id);
    setReplyMode(false);
    load();
  };

  const handleLike = async (postId: number) => {
    await likePost(postId);
    setLiked((s) => new Set(s).add(postId));
    load();
  };

  const handleUnlike = async (postId: number) => {
    await unlikePost(postId);
    setLiked((s) => {
      const n = new Set(s);
      n.delete(postId);
      return n;
    });
    load();
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
    if (!data) return;
    if (data.post.author_id === userId) {
      setReportTarget({ id: userId, nick: data.post.author_nick });
    } else {
      const reply = data.replies.find((r) => (r as { author_id?: number }).author_id === userId);
      setReportTarget({
        id: userId,
        nick: (reply as { author_nick: string })?.author_nick ?? '',
      });
    }
  };

  if (!id || isNaN(Number(id))) {
    navigate('/');
    return null;
  }

  if (loading || !data) {
    return (
      <div style={{ textAlign: 'center', padding: '2rem' }}>
        {loading ? 'Загрузка…' : 'Пост не найден'}
      </div>
    );
  }

  return (
    <>
      <PostCard
        post={data.post}
        onLike={handleLike}
        onUnlike={handleUnlike}
        onFavorite={handleFavorite}
        onUnfavorite={handleUnfavorite}
        isLiked={liked.has(data.post.id)}
        isFavorited={favorited.has(data.post.id)}
        onReport={handleReport}
      />
      {profile && (
        <>
          {replyMode ? (
            <PostComposer
              onSubmit={handleReply}
              placeholder="Напишите ответ..."
              replyTo={data.post.author_nick}
              onCancel={() => setReplyMode(false)}
            />
          ) : (
            <button
              type="button"
              onClick={() => setReplyMode(true)}
              style={{
                marginBottom: '1rem',
                padding: '0.5rem 1rem',
                background: 'var(--accent)',
                border: 'none',
                borderRadius: '8px',
                cursor: 'pointer',
              }}
            >
              Ответить
            </button>
          )}
        </>
      )}
      {data.replies.length > 0 && (
        <div style={{ marginTop: '1rem' }}>
          <h3 style={{ marginBottom: '0.75rem', fontSize: '1rem' }}>Ответы</h3>
          {data.replies.map((reply) => (
            <PostCard
              key={reply.id}
              post={{
                ...reply,
                author_id: (reply as { author_id?: number }).author_id,
                answer_to_id: data.post.id,
                replies_count: 0,
                author_avatar: null,
              }}
              compact
              onLike={handleLike}
              onUnlike={handleUnlike}
              onFavorite={handleFavorite}
              onUnfavorite={handleUnfavorite}
              isLiked={liked.has(reply.id)}
              isFavorited={favorited.has(reply.id)}
              onReport={(reply as { author_id?: number }).author_id ? handleReport : undefined}
            />
          ))}
        </div>
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
