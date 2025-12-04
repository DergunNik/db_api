-- Запросы для вставки данных (INSERT)
-- Данный файл содержит примеры SQL запросов для добавления новых записей во все таблицы системы

-- 1. Создание нового пользователя
-- Используется при регистрации нового пользователя
INSERT INTO "user" (nick, password_hash, is_admin)
VALUES ('new_user', '$2a$11$example.hash.here', false);

-- 2. Добавление медиа-файла
-- Используется при загрузке изображений/файлов
INSERT INTO media (file_path)
VALUES ('/uploads/avatar_123.jpg');

-- 3. Создание нового поста
-- Используется при публикации поста или ответа на пост
INSERT INTO post (text, author_id, answer_to_id)
VALUES ('Текст поста', 1, NULL); -- Основной пост

INSERT INTO post (text, author_id, answer_to_id)
VALUES ('Ответ на пост', 2, 1); -- Ответ на пост с ID=1

-- 4. Добавление лайка к посту
-- Используется при постановке лайка на пост
INSERT INTO "like" (post_id, user_id)
VALUES (1, 2)
ON CONFLICT (post_id, user_id) DO NOTHING; -- Предотвращает дублирование

-- 5. Добавление поста в избранное
-- Используется при добавлении поста в избранное
INSERT INTO favorite (user_id, post_id)
VALUES (2, 1)
ON CONFLICT (user_id, post_id) DO NOTHING; -- Предотвращает дублирование

-- 6. Создание подписки на пользователя
-- Используется при подписке на другого пользователя
INSERT INTO subscription (user_from_id, user_to_id)
VALUES (2, 1)
ON CONFLICT (user_from_id, user_to_id) DO NOTHING; -- Предотвращает дублирование

-- 7. Создание чата между пользователями
-- Используется при начале переписки между двумя пользователями
INSERT INTO chat (first_user_id, second_user_id)
VALUES (LEAST(1, 2), GREATEST(1, 2)) -- Гарантирует first_user_id < second_user_id
ON CONFLICT (first_user_id, second_user_id) DO NOTHING;

-- 8. Отправка сообщения в чат
-- Используется при отправке сообщения
INSERT INTO message (author_id, chat_id, content)
VALUES (1, 1, 'Текст сообщения');

-- 9. Создание жалобы
-- Используется при подаче жалобы на пользователя или пост
INSERT INTO report (author_id, target_user_id, comment)
VALUES (1, 2, 'Жалоба на поведение пользователя'); -- Жалоба на пользователя

INSERT INTO report (author_id, post_id, comment)
VALUES (1, 5, 'Жалоба на содержимое поста'); -- Жалоба на пост

-- 10. Выдача бана пользователю
-- Используется администратором при блокировке пользователя
INSERT INTO ban (banned_user_id, admin_id, report_id, end_date, reason)
VALUES (2, 1, 1, '2025-12-31 23:59:59', 'Нарушение правил сообщества');

-- 11. Запись в журнал событий
-- Используется для логирования действий пользователей
INSERT INTO log (user_id, details)
VALUES (1, 'Пользователь вошел в систему');

INSERT INTO log (details)
VALUES ('Система: Выполнена автоматическая очистка устаревших данных');
