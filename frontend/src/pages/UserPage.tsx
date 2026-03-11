import { useCallback, useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getProfile, getFollowers, getFollowing } from '../api/users';
import { getUserPosts } from '../api/posts';
import { getSubscriptionStatus } from '../api/subscriptions';
import { subscribe, unsubscribe } from '../api/subscriptions';
import type { UserSummary } from '../api/users';
import type { PostDetails } from '../api/posts';
import { PostCard } from '../components/PostCard';
import { ReportModal } from '../components/ReportModal';
import { useAuth } from '../context/AuthContext';
import { likePost, unlikePost, addFavorite, removeFavorite } from '../api/posts';

export function UserPage() {
  const { id } = useParams<{ id: string }>();
  const { profile: me } = useAuth();
  const [user, setUser] = useState<{
    id: number;
    nick: string;
    avatar_path: string | null;
    followers_count: number;
    following_count: number;
    posts_count: number;
  } | null>(null);
  const [posts, setPosts] = useState<PostDetails[]>([]);
  const [followers, setFollowers] = useState<UserSummary[]>([]);
  const [following, setFollowing] = useState<UserSummary[]>([]);
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<'posts' | 'followers' | 'following'>('posts');
  const [liked, setLiked] = useState<Set<number>>(new Set());
  const [favorited, setFavorited] = useState<Set<number>>(new Set());
  const [reportTarget, setReportTarget] = useState<{ id: number; nick: string } | null>(null);

  const userId = id ? Number(id) : null;

  const load = useCallback(async () => {
    if (!userId || isNaN(userId)) return;
    setLoading(true);
    try {
      const [postsRes, statusRes, followersRes, followingRes] = await Promise.all([
        getUserPosts(userId),
        me ? getSubscriptionStatus(userId) : Promise.resolve({ isSubscribed: false }),
        getFollowers(userId),
        getFollowing(userId),
      ]);
      setPosts(postsRes);
      if (statusRes) setIsSubscribed((statusRes as { isSubscribed: boolean }).isSubscribed);
      setFollowers(followersRes);
      setFollowing(followingRes);
      const nick = postsRes[0]?.author_nick ?? `user_${userId}`;
      const avatar = postsRes[0]?.author_avatar ?? null;
      setUser({
        id: userId,
        nick,
        avatar_path: avatar,
        followers_count: followersRes.length,
        following_count: followingRes.length,
        posts_count: postsRes.length,
      });
      if (me?.id === userId) {
        const p = await getProfile();
        setUser({
          id: p.id,
          nick: p.nick,
          avatar_path: p.avatar_path,
          followers_count: p.followers_count,
          following_count: p.following_count,
          posts_count: p.posts_count,
        });
      }
    } catch {
      setUser(null);
      setPosts([]);
    } finally {
      setLoading(false);
    }
  }, [userId, me]);

  useEffect(() => {
    load();
  }, [load]);

  const handleSubscribe = async () => {
    if (!userId) return;
    await subscribe(userId);
    setIsSubscribed(true);
    load();
  };

  const handleUnsubscribe = async () => {
    if (!userId) return;
    await unsubscribe(userId);
    setIsSubscribed(false);
    load();
  };

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

  const handleReport = (uid: number) => {
    const post = posts.find((p) => p.author_id === uid);
    setReportTarget({ id: uid, nick: post?.author_nick ?? `user_${uid}` });
  };

  if (!userId || isNaN(userId)) {
    return <div>Неверный ID пользователя</div>;
  }

  if (loading && !user) {
    return <div style={{ textAlign: 'center', padding: '2rem' }}>Загрузка…</div>;
  }

  const displayUser = user ?? {
    id: userId,
    nick: `user_${userId}`,
    avatar_path: null,
    followers_count: followers.length,
    following_count: following.length,
    posts_count: posts.length,
  };

  return (
    <>
      <div className="user-header" style={{ marginBottom: '1.5rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          {displayUser.avatar_path && (
            <img
              src={displayUser.avatar_path}
              alt=""
              style={{ width: 64, height: 64, borderRadius: '50%', objectFit: 'cover' }}
            />
          )}
          <div>
            <h2>@{displayUser.nick}</h2>
            <p style={{ color: 'var(--text-dim)', fontSize: '0.9rem' }}>
              Постов: {displayUser.posts_count} · Подписчиков: {displayUser.followers_count} · Подписок: {displayUser.following_count}
            </p>
            {me && me.id !== userId && (
              <button
                type="button"
                onClick={isSubscribed ? handleUnsubscribe : handleSubscribe}
                style={{
                  marginTop: '0.5rem',
                  padding: '0.4rem 1rem',
                  background: isSubscribed ? 'transparent' : 'var(--accent)',
                  border: `1px solid ${isSubscribed ? 'var(--border)' : 'transparent'}`,
                  borderRadius: '8px',
                  color: isSubscribed ? 'var(--text-dim)' : '#1a1b26',
                  cursor: 'pointer',
                }}
              >
                {isSubscribed ? 'Отписаться' : 'Подписаться'}
              </button>
            )}
          </div>
        </div>
      </div>
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem' }}>
        <button
          type="button"
          onClick={() => setTab('posts')}
          style={{
            padding: '0.4rem 0.75rem',
            background: tab === 'posts' ? 'var(--accent)' : 'transparent',
            border: '1px solid var(--border)',
            borderRadius: '6px',
            cursor: 'pointer',
          }}
        >
          Посты
        </button>
        <button
          type="button"
          onClick={() => setTab('followers')}
          style={{
            padding: '0.4rem 0.75rem',
            background: tab === 'followers' ? 'var(--accent)' : 'transparent',
            border: '1px solid var(--border)',
            borderRadius: '6px',
            cursor: 'pointer',
          }}
        >
          Подписчики
        </button>
        <button
          type="button"
          onClick={() => setTab('following')}
          style={{
            padding: '0.4rem 0.75rem',
            background: tab === 'following' ? 'var(--accent)' : 'transparent',
            border: '1px solid var(--border)',
            borderRadius: '6px',
            cursor: 'pointer',
          }}
        >
          Подписки
        </button>
      </div>
      {tab === 'posts' &&
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
        ))}
      {tab === 'followers' && (
        <ul style={{ listStyle: 'none', padding: 0 }}>
          {followers.map((u) => (
            <li key={u.id} style={{ padding: '0.5rem 0' }}>
              <Link to={`/user/${u.id}`} style={{ color: 'var(--accent)' }}>
                @{u.nick}
              </Link>
            </li>
          ))}
        </ul>
      )}
      {tab === 'following' && (
        <ul style={{ listStyle: 'none', padding: 0 }}>
          {following.map((u) => (
            <li key={u.id} style={{ padding: '0.5rem 0' }}>
              <Link to={`/user/${u.id}`} style={{ color: 'var(--accent)' }}>
                @{u.nick}
              </Link>
            </li>
          ))}
        </ul>
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
