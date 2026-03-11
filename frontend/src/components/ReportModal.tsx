import { useState } from 'react';
import { createReport } from '../api/reports';
import styles from './ReportModal.module.css';

interface ReportModalProps {
  targetUserId: number;
  targetNick: string;
  onClose: () => void;
  onSuccess?: () => void;
}

export function ReportModal({
  targetUserId,
  targetNick,
  onClose,
  onSuccess,
}: ReportModalProps) {
  const [comment, setComment] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!comment.trim()) {
      setError('Укажите причину жалобы');
      return;
    }
    setError('');
    setLoading(true);
    try {
      const { reportId } = await createReport(targetUserId, comment.trim());
      if (reportId) {
        alert(`Жалоба отправлена. ID репорта: ${reportId}. Сообщите его админу для рассмотрения.`);
      }
      onSuccess?.();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ошибка');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <h3>Жалоба на @{targetNick}</h3>
        <form onSubmit={handleSubmit}>
          <textarea
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder="Опишите причину жалобы..."
            className={styles.textarea}
            rows={4}
            disabled={loading}
          />
          {error && <div className={styles.error}>{error}</div>}
          <div className={styles.actions}>
            <button type="button" onClick={onClose} className={styles.cancel}>
              Отмена
            </button>
            <button type="submit" disabled={loading} className={styles.submit}>
              {loading ? '…' : 'Отправить'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
