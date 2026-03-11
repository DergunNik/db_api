import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { getChats, getMessages, sendMessage } from '../api/chats';
import type { ChatSummary, MessageDetails } from '../api/chats';
import { useAuth } from '../context/AuthContext';

export function ChatsPage() {
  const { id: chatIdParam } = useParams<{ id: string }>();
  const { profile } = useAuth();
  const [chats, setChats] = useState<ChatSummary[]>([]);
  const [messages, setMessages] = useState<MessageDetails[]>([]);
  const [loading, setLoading] = useState(true);
  const [messageText, setMessageText] = useState('');
  const [sending, setSending] = useState(false);

  const chatId = chatIdParam ? Number(chatIdParam) : null;

  const loadChats = useCallback(async () => {
    try {
      const data = await getChats();
      setChats(data);
    } catch {
      setChats([]);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadMessages = useCallback(async () => {
    if (!chatId) {
      setMessages([]);
      return;
    }
    try {
      const data = await getMessages(chatId);
      setMessages(data);
    } catch {
      setMessages([]);
    }
  }, [chatId]);

  useEffect(() => {
    loadChats();
  }, [loadChats]);

  useEffect(() => {
    loadMessages();
  }, [loadMessages]);

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!chatId || !messageText.trim() || sending) return;
    setSending(true);
    try {
      await sendMessage(chatId, messageText.trim());
      setMessageText('');
      loadMessages();
      loadChats();
    } catch {
      //
    } finally {
      setSending(false);
    }
  };

  if (!profile) {
    return (
      <div style={{ textAlign: 'center', padding: '2rem' }}>
        Войдите, чтобы видеть чаты
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', height: 'calc(100vh - 120px)', gap: '1rem' }}>
      <aside
        style={{
          width: 260,
          borderRight: '1px solid var(--border)',
          overflowY: 'auto',
          flexShrink: 0,
        }}
      >
        <h3 style={{ padding: '0.75rem' }}>Чаты</h3>
        {loading ? (
          <p style={{ padding: '1rem', color: 'var(--text-dim)' }}>Загрузка…</p>
        ) : chats.length === 0 ? (
          <p style={{ padding: '1rem', color: 'var(--text-dim)' }}>Нет чатов</p>
        ) : (
          <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
            {chats.map((c) => (
              <li key={c.id}>
                <Link
                  to={`/chats/${c.id}`}
                  style={{
                    display: 'block',
                    padding: '0.75rem 1rem',
                    color: c.id === chatId ? 'var(--accent)' : 'inherit',
                    textDecoration: 'none',
                    borderBottom: '1px solid var(--border)',
                  }}
                >
                  <strong>@{c.chat_with_nick}</strong>
                  {c.unread_count > 0 && (
                    <span
                      style={{
                        marginLeft: '0.5rem',
                        background: 'var(--accent)',
                        color: '#1a1b26',
                        padding: '0 6px',
                        borderRadius: 10,
                        fontSize: '0.8rem',
                      }}
                    >
                      {c.unread_count}
                    </span>
                  )}
                  {c.last_message && (
                    <div
                      style={{
                        fontSize: '0.85rem',
                        color: 'var(--text-dim)',
                        marginTop: '0.25rem',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {c.last_message}
                    </div>
                  )}
                </Link>
              </li>
            ))}
          </ul>
        )}
      </aside>
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        {chatId ? (
          <>
            <div
              style={{
                flex: 1,
                overflowY: 'auto',
                padding: '1rem',
                display: 'flex',
                flexDirection: 'column',
                gap: '0.5rem',
              }}
            >
              {messages.length === 0 ? (
                <p style={{ color: 'var(--text-dim)', textAlign: 'center' }}>
                  Нет сообщений
                </p>
              ) : (
                [...messages].reverse().map((m) => (
                  <div
                    key={m.id}
                    style={{
                      alignSelf: m.author_id === profile.id ? 'flex-end' : 'flex-start',
                      maxWidth: '70%',
                      padding: '0.5rem 0.75rem',
                      background:
                        m.author_id === profile.id
                          ? 'var(--accent)'
                          : 'var(--card-bg)',
                      color: m.author_id === profile.id ? '#1a1b26' : 'inherit',
                      borderRadius: 12,
                      borderBottomRightRadius: m.author_id === profile.id ? 4 : 12,
                      borderBottomLeftRadius: m.author_id === profile.id ? 12 : 4,
                    }}
                  >
                    <div style={{ fontSize: '0.8rem', opacity: 0.8 }}>
                      @{m.author_nick}
                    </div>
                    <div>{m.content}</div>
                    <div style={{ fontSize: '0.75rem', opacity: 0.7 }}>
                      {new Date(m.created_at).toLocaleString('ru')}
                    </div>
                  </div>
                ))
              )}
            </div>
            <form
              onSubmit={handleSend}
              style={{
                padding: '1rem',
                borderTop: '1px solid var(--border)',
                display: 'flex',
                gap: '0.5rem',
              }}
            >
              <input
                type="text"
                value={messageText}
                onChange={(e) => setMessageText(e.target.value)}
                placeholder="Сообщение..."
                style={{
                  flex: 1,
                  padding: '0.5rem 0.75rem',
                  border: '1px solid var(--border)',
                  borderRadius: 8,
                  background: 'var(--input-bg)',
                  color: 'var(--text)',
                }}
              />
              <button
                type="submit"
                disabled={sending || !messageText.trim()}
                style={{
                  padding: '0.5rem 1rem',
                  background: 'var(--accent)',
                  border: 'none',
                  borderRadius: 8,
                  cursor: 'pointer',
                }}
              >
                Отправить
              </button>
            </form>
          </>
        ) : (
          <div
            style={{
              flex: 1,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--text-dim)',
            }}
          >
            Выберите чат или найдите пользователя, чтобы начать
          </div>
        )}
      </div>
    </div>
  );
}
