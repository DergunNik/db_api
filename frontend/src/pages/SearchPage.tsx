import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { searchUsers } from '../api/users';
import type { UserSearchResult } from '../api/users';
import { getOrCreateChat } from '../api/chats';

export function SearchPage() {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [users, setUsers] = useState<UserSearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [creatingChat, setCreatingChat] = useState<number | null>(null);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim() || query.length < 2) return;
    setLoading(true);
    try {
      const data = await searchUsers(query.trim(), 20);
      setUsers(data);
    } catch {
      setUsers([]);
    } finally {
      setLoading(false);
    }
  };

  const handleStartChat = async (userId: number) => {
    setCreatingChat(userId);
    try {
      const { chatId } = await getOrCreateChat(userId);
      navigate(`/chats/${chatId}`);
    } catch {
      //
    } finally {
      setCreatingChat(null);
    }
  };

  return (
    <div>
      <h2 style={{ marginBottom: '1rem' }}>Поиск пользователей</h2>
      <form onSubmit={handleSearch} style={{ marginBottom: '1rem', display: 'flex', gap: '0.5rem' }}>
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Ник (минимум 2 символа)"
          style={{
            flex: 1,
            padding: '0.5rem 0.75rem',
            border: '1px solid var(--border)',
            borderRadius: 8,
            background: 'var(--input-bg)',
            color: 'var(--text)',
          }}
          minLength={2}
        />
        <button
          type="submit"
          disabled={loading}
          style={{
            padding: '0.5rem 1rem',
            background: 'var(--accent)',
            border: 'none',
            borderRadius: 8,
            cursor: 'pointer',
          }}
        >
          Искать
        </button>
      </form>
      {loading ? (
        <p>Загрузка…</p>
      ) : (
        <ul style={{ listStyle: 'none', padding: 0 }}>
          {users.map((u) => (
            <li
              key={u.id}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '0.75rem',
                border: '1px solid var(--border)',
                borderRadius: 8,
                marginBottom: '0.5rem',
              }}
            >
              <Link to={`/user/${u.id}`} style={{ color: 'var(--accent)' }}>
                @{u.nick}
              </Link>
              <button
                type="button"
                onClick={() => handleStartChat(u.id)}
                disabled={creatingChat === u.id}
                style={{
                  padding: '0.35rem 0.75rem',
                  background: 'var(--accent)',
                  border: 'none',
                  borderRadius: 6,
                  cursor: 'pointer',
                  fontSize: '0.9rem',
                }}
              >
                Написать
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
