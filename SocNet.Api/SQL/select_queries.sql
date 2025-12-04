-- Запросы для выборки данных (SELECT)
-- Данный файл содержит примеры SQL запросов для получения данных из системы

-- 1. Пользователи

-- Получение информации о пользователе по ID
SELECT id, nick, avatar_id, is_admin, created_at
FROM "user"
WHERE id = 1;

-- Поиск пользователей по нику (с частичным совпадением)
SELECT id, nick, avatar_id, is_admin, created_at
FROM "user"
WHERE nick ILIKE '%search_term%'
ORDER BY nick;

-- Получение списка всех администраторов
SELECT id, nick, created_at
FROM "user"
WHERE is_admin = true
ORDER BY created_at DESC;

-- Получение пользователей с аватарами
SELECT u.id, u.nick, m.file_path as avatar_path, u.created_at
FROM "user" u
LEFT JOIN media m ON u.avatar_id = m.id
WHERE u.avatar_id IS NOT NULL;

-- 2. Посты

-- Получение постов пользователя (с пагинацией)
SELECT id, text, answer_to_id, created_at
FROM post
WHERE author_id = 1
ORDER BY created_at DESC
LIMIT 20 OFFSET 0;

-- Получение ответов на конкретный пост
SELECT p.id, p.text, p.created_at, u.nick as author_nick
FROM post p
JOIN "user" u ON p.author_id = u.id
WHERE p.answer_to_id = 1
ORDER BY p.created_at ASC;

-- Получение ленты постов (посты от пользователей на которых подписан текущий пользователь)
SELECT DISTINCT p.id, p.text, p.created_at, u.nick as author_nick
FROM post p
JOIN "user" u ON p.author_id = u.id
JOIN subscription s ON p.author_id = s.user_to_id
WHERE s.user_from_id = 1
ORDER BY p.created_at DESC
LIMIT 50;

-- 3. Лайки и избранное

-- Подсчет лайков для поста
SELECT COUNT(*) as likes_count
FROM "like"
WHERE post_id = 1;

-- Проверка, поставил ли пользователь лайк на пост
SELECT EXISTS(
    SELECT 1 FROM "like"
    WHERE post_id = 1 AND user_id = 2
) as has_liked;

-- Получение постов, которые пользователь добавил в избранное
SELECT p.id, p.text, p.created_at, f.created_at as favorited_at
FROM post p
JOIN favorite f ON p.id = f.post_id
WHERE f.user_id = 1
ORDER BY f.created_at DESC;

-- 4. Подписки

-- Получение списка подписок пользователя (на кого подписан)
SELECT u.id, u.nick, s.created_at as subscribed_at
FROM "user" u
JOIN subscription s ON u.id = s.user_to_id
WHERE s.user_from_id = 1
ORDER BY s.created_at DESC;

-- Получение списка подписчиков пользователя (кто подписан на пользователя)
SELECT u.id, u.nick, s.created_at as subscribed_at
FROM "user" u
JOIN subscription s ON u.id = s.user_from_id
WHERE s.user_to_id = 1
ORDER BY s.created_at DESC;

-- Подсчет количества подписчиков
SELECT COUNT(*) as followers_count
FROM subscription
WHERE user_to_id = 1;

-- Подсчет количества подписок
SELECT COUNT(*) as following_count
FROM subscription
WHERE user_from_id = 1;

-- 5. Чаты и сообщения

-- Получение списка чатов пользователя
SELECT c.id,
       CASE WHEN c.first_user_id = 1 THEN u2.nick ELSE u1.nick END as chat_with_nick,
       CASE WHEN c.first_user_id = 1 THEN u2.id ELSE u1.id END as chat_with_id,
       c.created_at as chat_created_at,
       (SELECT content FROM message WHERE chat_id = c.id ORDER BY created_at DESC LIMIT 1) as last_message,
       (SELECT created_at FROM message WHERE chat_id = c.id ORDER BY created_at DESC LIMIT 1) as last_message_time
FROM chat c
JOIN "user" u1 ON c.first_user_id = u1.id
JOIN "user" u2 ON c.second_user_id = u2.id
WHERE c.first_user_id = 1 OR c.second_user_id = 1
ORDER BY last_message_time DESC NULLS LAST;

-- Получение сообщений из чата (с пагинацией)
SELECT m.id, m.content, m.created_at, u.nick as author_nick
FROM message m
JOIN "user" u ON m.author_id = u.id
WHERE m.chat_id = 1
ORDER BY m.created_at DESC
LIMIT 50 OFFSET 0;

-- 6. Жалобы и баны

-- Получение активных банов пользователя
SELECT b.id, b.start_date, b.end_date, b.reason, u.nick as admin_nick
FROM ban b
LEFT JOIN "user" u ON b.admin_id = u.id
WHERE b.banned_user_id = 1
  AND (b.end_date IS NULL OR b.end_date > now())
ORDER BY b.start_date DESC;

-- Получение всех жалоб (для администратора)
SELECT r.id, r.comment, r.is_reviewed, r.created_at,
       ua.nick as author_nick,
       ut.nick as target_user_nick,
       p.text as post_text
FROM report r
LEFT JOIN "user" ua ON r.author_id = ua.id
LEFT JOIN "user" ut ON r.target_user_id = ut.id
LEFT JOIN post p ON r.post_id = p.id
ORDER BY r.created_at DESC;

-- Получение числа нерассмотренных жалоб
SELECT COUNT(*) as pending_reports
FROM report
WHERE is_reviewed = false;

-- 7. Статистика и аналитика

-- Статистика по пользователям
SELECT
    COUNT(*) as total_users,
    COUNT(CASE WHEN is_admin THEN 1 END) as admin_count,
    COUNT(CASE WHEN created_at >= now() - interval '30 days' THEN 1 END) as new_users_30d
FROM "user";

-- Статистика по постам
SELECT
    COUNT(DISTINCT p.id) as total_posts,
    COUNT(DISTINCT l.post_id) as posts_with_likes,
    COUNT(DISTINCT f.post_id) as posts_in_favorites,
FROM post p
LEFT JOIN "like" l ON p.id = l.post_id
LEFT JOIN favorite f ON p.id = f.post_id;

-- 8. Логи

-- Получение логов действий пользователя
SELECT time, details
FROM log
WHERE user_id = 1
ORDER BY time DESC
LIMIT 100;

-- Получение системных логов за период
SELECT time, details
FROM log
WHERE user_id IS NULL
  AND time >= '2025-01-01' AND time < '2025-02-01'
ORDER BY time DESC;
