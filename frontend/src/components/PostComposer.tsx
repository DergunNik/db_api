import { useState } from 'react';
import styles from './PostComposer.module.css';

interface PostComposerProps {
  onSubmit: (text: string) => Promise<void>;
  placeholder?: string;
  replyTo?: string;
  onCancel?: () => void;
}

export function PostComposer({
  onSubmit,
  placeholder = 'Что у вас нового?',
  replyTo,
  onCancel,
}: PostComposerProps) {
  const [text, setText] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const t = text.trim();
    if (!t) {
      setError('Напишите текст поста');
      return;
    }
    setError('');
    setLoading(true);
    try {
      await onSubmit(t);
      setText('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ошибка');
    } finally {
      setLoading(false);
    }
  };

  return (
    <form className={styles.form} onSubmit={handleSubmit}>
      {replyTo && (
        <div className={styles.replyLabel}>Ответ: @{replyTo}</div>
      )}
      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder={placeholder}
        className={styles.textarea}
        rows={3}
        disabled={loading}
      />
      {error && <div className={styles.error}>{error}</div>}
      <div className={styles.footer}>
        {onCancel && (
          <button type="button" onClick={onCancel} className={styles.cancel}>
            Отмена
          </button>
        )}
        <button type="submit" disabled={loading || !text.trim()} className={styles.submit}>
          {loading ? '…' : 'Опубликовать'}
        </button>
      </div>
    </form>
  );
}
