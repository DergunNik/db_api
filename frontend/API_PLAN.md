# План интеграции фронтенда с SocNet API

## 1. Изучение API (повторно)

### Базовый URL
- API: `http://localhost:8080` (или 8081)
- JWT в заголовке: `Authorization: Bearer <token>`

### Auth (`/auth`)
| Метод | Путь | Тело | Ответ |
|-------|------|------|-------|
| POST | /register | `{ nick, password }` | 201 / 409 |
| POST | /login | `{ nick, password }` | `{ token }` / 400 |
| POST | /logout | - | 200 (Bearer) |

### Посты (`/posts`) — все требуют Bearer
| Метод | Путь | Тело/Query | Ответ |
|-------|------|------------|-------|
| POST | / | `{ text, answer_to_id?, media_id? }` | 201 + Post |
| GET | /{postId} | - | `{ post, replies }` |
| GET | /feed | page, pageSize | PostDetails[] |
| GET | /user/{userId} | page, pageSize | PostDetails[] |
| PUT | /{postId} | `{ text, media_id? }` | 200 |
| DELETE | /{postId} | - | 200 |
| POST | /{postId}/like | - | 200 |
| DELETE | /{postId}/like | - | 200 |
| POST | /{postId}/favorite | - | 200 |
| DELETE | /{postId}/favorite | - | 200 |
| GET | /favorites | page, pageSize | PostDetails[] |

**PostDetails**: id, text, answer_to_id, created_at, author_nick, author_id, author_avatar, post_media, likes_count, replies_count

### Пользователи (`/users`) — Bearer
| Метод | Путь | Тело/Query | Ответ |
|-------|------|------------|-------|
| GET | /profile | - | UserProfile |
| GET | /search | query, limit | UserSearchResult[] |
| PUT | /profile | `{ nick?, avatar_id? }` | 200 |
| GET | /{userId}/followers | page, pageSize | UserSummary[] |
| GET | /{userId}/following | page, pageSize | UserSummary[] |

**UserProfile**: id, nick, is_admin, created_at, avatar_path, followers_count, following_count, posts_count

### Подписки (`/subscriptions`) — Bearer
| Метод | Путь | Ответ |
|-------|------|-------|
| POST | /{targetUserId} | 200 |
| DELETE | /{targetUserId} | 200 |
| GET | /{targetUserId}/status | `{ isSubscribed }` |
| GET | /counts | `{ followers_count, following_count }` |

### Репорты (`/reports`) — Bearer
| Метод | Путь | Тело | Ответ |
|-------|------|------|-------|
| POST | /user/{targetUserId} | `{ comment }` | 201 + { reportId } |
| GET | /{reportId} | - | ReportDetails (автор или админ) |

### Чаты (`/chats`) — Bearer
| Метод | Путь | Тело | Ответ |
|-------|------|------|-------|
| GET | / | - | ChatSummary[] |
| PUT | /with/{targetUserId} | - | `{ chatId }` или 201 |
| GET | /{chatId}/messages | - | MessageDetails[] |
| POST | /{chatId}/messages | `{ content, media_id? }` | 201 + MessageDetails |
| PUT | /{chatId}/messages/{messageId} | `{ content, media_id? }` | 200 |
| DELETE | /{chatId}/messages/{messageId} | - | 200 |

### Админ (`/admin/*`) — Bearer + Role: Admin
| Метод | Путь | Тело/Query | Ответ |
|-------|------|------------|-------|
| GET | /admin/users | page, pageSize | UserAdminView[] |
| PUT | /admin/users/{userId}/role | `{ is_admin }` | 200 |
| DELETE | /admin/users/{userId} | - | 200 |
| POST | /admin/reports/{reportId}/ban | `{ end_date?, reason }` | 201 + { banId } |
| DELETE | /admin/reports/bans/{banId} | - | 200 |
| GET | /admin/reports/bans | page | BanDetails[] |
| GET | /admin/analytics/top-users | format | JSON/CSV |
| GET | /admin/analytics/timeline | format | JSON/CSV |
| GET | /admin/analytics/crud-stats | format | JSON/CSV |
| GET | /admin/analytics/anomalies | format | JSON/CSV |
| GET | /admin/analytics/hourly-trends | format | JSON/CSV |
| GET | /admin/logs | userId, type, windowMinutes, from, to, page, pageSize | { filters, pagination, logs } |
| GET | /admin/logs/user/{userId} | page, pageSize | { userId, total, logs } |
| GET | /admin/logs/errors | page, pageSize | { total, logs } |

**Примечание**: API не имеет GET /admin/reports для списка всех репортов. Есть только bans.
