import { useCallback, useEffect, useState } from 'react';
import {
  getAdminUsers,
  getBans,
  setUserRole,
  deleteUser,
  unban,
  banFromReport,
  getAnalyticsTopUsers,
  getAnalyticsTimeline,
  getAnalyticsCrudStats,
  getLogs,
} from '../api/admin';
import type { UserAdminView, BanDetails } from '../api/admin';
import { useAuth } from '../context/AuthContext';

export function AdminPage() {
  const { profile } = useAuth();
  const [tab, setTab] = useState<'users' | 'bans' | 'banReport' | 'analytics' | 'logs'>('users');
  const [banReportId, setBanReportId] = useState('');
  const [banReportReason, setBanReportReason] = useState('');
  const [banReportLoading, setBanReportLoading] = useState(false);
  const [users, setUsers] = useState<UserAdminView[]>([]);
  const [bans, setBans] = useState<BanDetails[]>([]);
  const [analytics, setAnalytics] = useState<Record<string, unknown>>({});
  const [logs, setLogs] = useState<{ logs: unknown[]; pagination?: unknown }>({ logs: [] });
  const [loading, setLoading] = useState(true);

  const loadUsers = useCallback(async () => {
    try {
      const data = await getAdminUsers(1, 50);
      setUsers(data);
    } catch {
      setUsers([]);
    }
  }, []);

  const loadBans = useCallback(async () => {
    try {
      const data = await getBans(1);
      setBans(data);
    } catch {
      setBans([]);
    }
  }, []);

  const loadAnalytics = useCallback(async () => {
    try {
      const [top, timeline, crud] = await Promise.all([
        getAnalyticsTopUsers(),
        getAnalyticsTimeline(),
        getAnalyticsCrudStats(),
      ]);
      setAnalytics({ topUsers: top, timeline, crudStats: crud });
    } catch {
      setAnalytics({});
    }
  }, []);

  const loadLogs = useCallback(async () => {
    try {
      const data = await getLogs({ page: 1, pageSize: 50 });
      setLogs({ logs: data.logs, pagination: data.pagination });
    } catch {
      setLogs({ logs: [] });
    }
  }, []);

  useEffect(() => {
    setLoading(true);
    if (tab === 'users') loadUsers().finally(() => setLoading(false));
    else if (tab === 'bans') loadBans().finally(() => setLoading(false));
    else if (tab === 'banReport') setLoading(false);
    else if (tab === 'analytics') loadAnalytics().finally(() => setLoading(false));
    else if (tab === 'logs') loadLogs().finally(() => setLoading(false));
  }, [tab, loadUsers, loadBans, loadAnalytics, loadLogs]);

  const handleBanFromReport = async (e: React.FormEvent) => {
    e.preventDefault();
    const reportId = Number(banReportId);
    if (!reportId || !banReportReason.trim()) return;
    setBanReportLoading(true);
    try {
      await banFromReport(reportId, banReportReason.trim());
      setBanReportId('');
      setBanReportReason('');
    } finally {
      setBanReportLoading(false);
    }
  };

  const handleSetRole = async (userId: number, is_admin: boolean) => {
    await setUserRole(userId, is_admin);
    loadUsers();
  };

  const handleDeleteUser = async (userId: number) => {
    if (!confirm('Удалить пользователя?')) return;
    await deleteUser(userId);
    loadUsers();
  };

  const handleUnban = async (banId: number) => {
    await unban(banId);
    loadBans();
  };

  if (!profile?.is_admin) {
    return (
      <div style={{ textAlign: 'center', padding: '2rem' }}>
        Доступ запрещён
      </div>
    );
  }

  return (
    <>
      <h2 style={{ marginBottom: '1rem' }}>Админка</h2>
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.5rem' }}>
        {(['users', 'bans', 'banReport', 'analytics', 'logs'] as const).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            style={{
              padding: '0.4rem 0.75rem',
              background: tab === t ? 'var(--accent)' : 'transparent',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              cursor: 'pointer',
            }}
          >
            {t === 'users' && 'Пользователи'}
            {t === 'bans' && 'Баны'}
            {t === 'banReport' && 'Бан по репорту'}
            {t === 'analytics' && 'Аналитика'}
            {t === 'logs' && 'Логи'}
          </button>
        ))}
      </div>
      {loading ? (
        <p>Загрузка…</p>
      ) : tab === 'users' ? (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'left', padding: '0.5rem' }}>ID</th>
                <th style={{ textAlign: 'left', padding: '0.5rem' }}>Ник</th>
                <th style={{ textAlign: 'left', padding: '0.5rem' }}>Админ</th>
                <th style={{ textAlign: 'left', padding: '0.5rem' }}>Бан</th>
                <th style={{ textAlign: 'left', padding: '0.5rem' }}>Действия</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ padding: '0.5rem' }}>{u.id}</td>
                  <td style={{ padding: '0.5rem' }}>@{u.nick}</td>
                  <td style={{ padding: '0.5rem' }}>{u.is_admin ? 'Да' : 'Нет'}</td>
                  <td style={{ padding: '0.5rem' }}>
                    {u.is_banned ? `Да: ${u.ban_reason ?? ''}` : 'Нет'}
                  </td>
                  <td style={{ padding: '0.5rem' }}>
                    {u.id !== profile.id && (
                      <>
                        <button
                          type="button"
                          onClick={() => handleSetRole(u.id, !u.is_admin)}
                          style={{ marginRight: '0.5rem', fontSize: '0.85rem' }}
                        >
                          {u.is_admin ? 'Убрать админ' : 'Сделать админом'}
                        </button>
                        <button
                          type="button"
                          onClick={() => handleDeleteUser(u.id)}
                          style={{ fontSize: '0.85rem', color: '#f7768e' }}
                        >
                          Удалить
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : tab === 'banReport' ? (
        <form onSubmit={handleBanFromReport} style={{ maxWidth: 400 }}>
          <p style={{ color: 'var(--text-dim)', marginBottom: '1rem' }}>
            Введите ID репорта (его получает автор жалобы при отправке)
          </p>
          <input
            type="number"
            placeholder="ID репорта"
            value={banReportId}
            onChange={(e) => setBanReportId(e.target.value)}
            style={{
              width: '100%',
              padding: '0.5rem',
              marginBottom: '0.5rem',
              border: '1px solid var(--border)',
              borderRadius: 8,
              background: 'var(--input-bg)',
            }}
          />
          <input
            type="text"
            placeholder="Причина бана"
            value={banReportReason}
            onChange={(e) => setBanReportReason(e.target.value)}
            style={{
              width: '100%',
              padding: '0.5rem',
              marginBottom: '0.5rem',
              border: '1px solid var(--border)',
              borderRadius: 8,
              background: 'var(--input-bg)',
            }}
          />
          <button type="submit" disabled={banReportLoading}>
            Забанить по репорту
          </button>
        </form>
      ) : tab === 'bans' ? (
        <div>
          {bans.length === 0 ? (
            <p style={{ color: 'var(--text-dim)' }}>Нет активных банов</p>
          ) : (
            <ul style={{ listStyle: 'none', padding: 0 }}>
              {bans.map((b) => (
                <li
                  key={b.id}
                  style={{
                    padding: '0.75rem',
                    border: '1px solid var(--border)',
                    borderRadius: 8,
                    marginBottom: '0.5rem',
                  }}
                >
                  <strong>@{b.banned_user_nick}</strong> — {b.reason ?? 'без причины'}
                  <br />
                  <small style={{ color: 'var(--text-dim)' }}>
                    С {new Date(b.start_date).toLocaleString('ru')}
                    {b.end_date && ` до ${new Date(b.end_date).toLocaleString('ru')}`}
                  </small>
                  <br />
                  <button
                    type="button"
                    onClick={() => handleUnban(b.id)}
                    style={{ marginTop: '0.5rem', fontSize: '0.85rem' }}
                  >
                    Разбанить
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      ) : tab === 'analytics' ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <pre
            style={{
              background: 'var(--card-bg)',
              padding: '1rem',
              borderRadius: 8,
              overflow: 'auto',
              fontSize: '0.85rem',
            }}
          >
            {JSON.stringify(analytics, null, 2)}
          </pre>
        </div>
      ) : (
        <div>
          <pre
            style={{
              background: 'var(--card-bg)',
              padding: '1rem',
              borderRadius: 8,
              overflow: 'auto',
              fontSize: '0.8rem',
              maxHeight: 400,
            }}
          >
            {JSON.stringify(logs.logs, null, 2)}
          </pre>
        </div>
      )}
    </>
  );
}
