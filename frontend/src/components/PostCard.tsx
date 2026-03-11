import { Link } from 'react-router-dom';
import type { PostDetails } from '../api/posts';
import { useAuth } from '../context/AuthContext';
import styles from './PostCard.module.css';

interface PostCardProps {
  post: PostDetails | (Partial<PostDetails> & { id: number; author_nick: string; created_at: string; likes_count: number });
  onLike?: (postId: number) => void;
  onUnlike?: (postId: number) => void;
  onFavorite?: (postId: number) => void;
  onUnfavorite?: (postId: number) => void;
  isLiked?: boolean;
  isFavorited?: boolean;
  onReport?: (userId: number) => void;
  compact?: boolean;
}

export function PostCard({
  post,
  onLike,
  onUnlike,
  onFavorite,
  onUnfavorite,
  isLiked = false,
  isFavorited = false,
  onReport,
  compact = false,
}: PostCardProps) {
  const { profile } = useAuth();
  const myId = profile?.id;

  const formatDate = (s: string) => {
    const d = new Date(s);
    const now = new Date();
    const diff = now.getTime() - d.getTime();
    if (diff < 60000) return 'только что';
    if (diff < 3600000) return `${Math.floor(diff / 60000)} мин.`;
    if (diff < 86400000) return `${Math.floor(diff / 3600000)} ч.`;
    return d.toLocaleDateString('ru');
  };

  return (
    <article className={`${styles.card} ${compact ? styles.compact : ''}`}>
      <div className={styles.header}>
        {post.author_id ? (
          <Link to={`/user/${post.author_id}`} className={styles.author}>
            {post.author_avatar && (
              <img
                src={post.author_avatar}
                alt=""
                className={styles.avatar}
                onError={(e) => {
                  (e.target as HTMLImageElement).style.display = 'none';
                }}
              />
            )}
            <span className={styles.nick}>@{post.author_nick}</span>
          </Link>
        ) : (
          <span className={styles.author}>
            <span className={styles.nick}>@{post.author_nick}</span>
          </span>
        )}
        <span className={styles.date}>{formatDate(post.created_at)}</span>
        {myId && post.author_id && post.author_id !== myId && onReport && (
          <button
            type="button"
            className={styles.reportBtn}
            onClick={() => { if (post.author_id && onReport) onReport(post.author_id); }}
            title="Пожаловаться"
          >
            ⚠
          </button>
        )}
      </div>
      {post.answer_to_id && (
        <div className={styles.replyTo}>
          <Link to={`/post/${post.answer_to_id}`}>Ответ на пост</Link>
        </div>
      )}
      <p className={styles.text}>{post.text || '(пустой пост)'}</p>
      {post.post_media && (
        <div className={styles.media}>
          <img src={post.post_media} alt="" />
        </div>
      )}
      <div className={styles.actions}>
        <Link to={`/post/${post.id}`} className={styles.replies}>
          💬 {(post as PostDetails).replies_count ?? 0}
        </Link>
        {myId && (
          <>
            <button
              type="button"
              className={isLiked ? styles.liked : ''}
              onClick={() => (isLiked ? onUnlike?.(post.id) : onLike?.(post.id))}
            >
              ❤ {post.likes_count}
            </button>
            <button
              type="button"
              className={isFavorited ? styles.favorited : ''}
              onClick={() =>
                isFavorited ? onUnfavorite?.(post.id) : onFavorite?.(post.id)
              }
            >
              ★
            </button>
          </>
        )}
      </div>
    </article>
  );
}
