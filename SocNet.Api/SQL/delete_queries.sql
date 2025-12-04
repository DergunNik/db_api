-- Запросы для удаления данных (DELETE)
-- Данный файл содержит примеры SQL запросов для удаления записей из таблиц
-- ВНИМАНИЕ: Многие удаления выполняются каскадно через FOREIGN KEY constraints

-- 1. Удаление пользователя
-- Полное удаление пользователя и всех связанных данных (каскадное удаление)
DELETE FROM "user" WHERE id = 1;

-- 2. Удаление поста
-- Удаление поста и всех связанных лайков, избранного, ответов (каскадное удаление)
DELETE FROM post WHERE id = 1;

-- 3. Удаление медиа-файла
-- Удаление файла из базы данных
DELETE FROM media WHERE id = 5;

-- 4. Снятие лайка с поста
-- Удаление конкретного лайка
DELETE FROM "like" WHERE post_id = 1 AND user_id = 2;

-- 5. Удаление поста из избранного
-- Удаление из избранного конкретного поста
DELETE FROM favorite WHERE user_id = 2 AND post_id = 1;

-- 6. Отписка от пользователя
-- Удаление подписки
DELETE FROM subscription WHERE user_from_id = 2 AND user_to_id = 1;

-- 7. Удаление чата
-- Удаление чата и всех сообщений в нем (каскадное удаление)
DELETE FROM chat WHERE id = 1;

-- 8. Удаление сообщения
-- Удаление конкретного сообщения из чата
DELETE FROM message WHERE id = 10;

-- 9. Удаление жалобы
-- Удаление жалобы (если она не привела к бану)
DELETE FROM report WHERE id = 3;

-- 10. Удаление бана
-- Снятие бана с пользователя
DELETE FROM ban WHERE id = 2;

-- 11. Удаление записей из лога
-- Удаление старых записей лога (старше 1 года)
DELETE FROM log WHERE time < now() - interval '1 year';

-- 12. Массовые удаления
-- Удаление всех неиспользуемых медиа-файлов (без ссылок)
DELETE FROM media
WHERE id NOT IN (
    SELECT DISTINCT avatar_id FROM "user" WHERE avatar_id IS NOT NULL
    UNION
    SELECT DISTINCT file_path FROM post WHERE file_path IS NOT NULL
    UNION
    SELECT DISTINCT file_path FROM message WHERE file_path IS NOT NULL
);

-- Удаление пустых чатов (без сообщений)
DELETE FROM chat
WHERE id NOT IN (
    SELECT DISTINCT chat_id FROM message
);

-- Удаление истекших банов (старше 30 дней после окончания)
DELETE FROM ban
WHERE end_date < now() - interval '30 days';

-- 13. Условные удаления
-- Удаление постов пользователя, которые старше 6 месяцев и не имеют лайков/ответов
DELETE FROM post
WHERE author_id = 1
  AND created_at < now() - interval '6 months'
  AND id NOT IN (SELECT post_id FROM "like")
  AND id NOT IN (SELECT answer_to_id FROM post WHERE answer_to_id IS NOT NULL);
