-- Тестовые данные для социальной сети
-- Этот файл содержит примеры данных для тестирования системы

BEGIN;

-- Очистка существующих данных (для повторного запуска)
TRUNCATE TABLE log CASCADE;
TRUNCATE TABLE ban CASCADE;
TRUNCATE TABLE report CASCADE;
TRUNCATE TABLE message CASCADE;
TRUNCATE TABLE chat_read_status CASCADE;
TRUNCATE TABLE chat CASCADE;
TRUNCATE TABLE subscription CASCADE;
TRUNCATE TABLE favorite CASCADE;
TRUNCATE TABLE "like" CASCADE;
TRUNCATE TABLE post CASCADE;
TRUNCATE TABLE media CASCADE;
TRUNCATE TABLE "user" CASCADE;

-- Создание тестовых пользователей
INSERT INTO "user" (nick, password_hash, is_admin, created_at) VALUES
('admin', '$2a$11$admin.hash.here', true, '2025-01-01 00:00:00+00'),
('alice', '$2a$11$alice.hash.here', false, '2025-01-02 00:00:00+00'),
('bob', '$2a$11$bob.hash.here', false, '2025-01-03 00:00:00+00'),
('charlie', '$2a$11$charlie.hash.here', false, '2025-01-04 00:00:00+00'),
('diana', '$2a$11$diana.hash.here', false, '2025-01-05 00:00:00+00'),
('eve', '$2a$11$eve.hash.here', false, '2025-01-06 00:00:00+00');

-- Создание медиа-файлов
INSERT INTO media (file_path) VALUES
('/uploads/admin-1.jpg'), -- ID 1
('/uploads/user-1.jpg'),  -- ID 2
('/uploads/user-2.jpg'),  -- ID 3
('/uploads/user-1.jpg'),  -- ID 4
('/uploads/user-2.jpg'),  -- ID 5
('/uploads/post-1.jpg'),  -- ID 6
('/uploads/post-2.jpg'),  -- ID 7
('/uploads/chat-1.png');  -- ID 8

-- Обновление аватаров пользователей
UPDATE "user" SET avatar_id = 1 WHERE nick = 'admin';
UPDATE "user" SET avatar_id = 2 WHERE nick = 'alice';
UPDATE "user" SET avatar_id = 3 WHERE nick = 'bob';
UPDATE "user" SET avatar_id = 4 WHERE nick = 'charlie';
UPDATE "user" SET avatar_id = 5 WHERE nick = 'diana';

-- Создание постов
INSERT INTO post (text, author_id, created_at) VALUES
('Привет всем! Это мой первый пост в социальной сети.', 2, '2025-01-02 10:00:00+00'),
('Сегодня прекрасная погода!', 3, '2025-01-03 11:00:00+00'),
('Делюсь интересной статьей о технологиях.', 4, '2025-01-04 12:00:00+00'),
('Как прошел ваш день?', 5, '2025-01-05 13:00:00+00'),
('Новый пост с изображением!', 2, '2025-01-02 14:00:00+00'),
('Ответ на пост Алисы', 3, '2025-01-03 15:00:00+00'),
('Еще один ответ', 4, '2025-01-04 16:00:00+00');

-- Установка связей между постами (ответы)
UPDATE post SET answer_to_id = 1 WHERE id = 6;
UPDATE post SET answer_to_id = 1 WHERE id = 7;

-- Добавление изображений
UPDATE post SET media_id = 6 WHERE id = 4;
UPDATE post SET media_id = 7 WHERE id = 5;

-- Добавление лайков
INSERT INTO "like" (post_id, user_id, created_at) VALUES
(1, 3, '2025-01-03 11:30:00+00'),
(1, 4, '2025-01-04 12:30:00+00'),
(2, 2, '2025-01-02 14:30:00+00'),
(3, 2, '2025-01-02 15:00:00+00'),
(3, 5, '2025-01-05 13:30:00+00'),
(5, 3, '2025-01-03 16:00:00+00');

-- Добавление постов в избранное
INSERT INTO favorite (user_id, post_id, created_at) VALUES
(2, 3, '2025-01-04 13:00:00+00'),
(3, 1, '2025-01-03 12:00:00+00'),
(4, 2, '2025-01-03 12:30:00+00'),
(5, 1, '2025-01-05 14:00:00+00');

-- Создание подписок
INSERT INTO subscription (user_from_id, user_to_id, created_at) VALUES
(2, 3, '2025-01-02 16:00:00+00'), -- Alice подписана на Bob
(2, 4, '2025-01-02 16:30:00+00'), -- Alice подписана на Charlie
(3, 2, '2025-01-03 17:00:00+00'), -- Bob подписан на Alice
(3, 5, '2025-01-03 17:30:00+00'), -- Bob подписан на Diana
(4, 2, '2025-01-04 18:00:00+00'), -- Charlie подписан на Alice
(5, 2, '2025-01-05 19:00:00+00'), -- Diana подписана на Alice
(5, 3, '2025-01-05 19:30:00+00'); -- Diana подписана на Bob

-- Создание чатов
INSERT INTO chat (first_user_id, second_user_id, created_at) VALUES
(2, 3, '2025-01-02 20:00:00+00'), -- Alice и Bob
(2, 4, '2025-01-04 21:00:00+00'), -- Alice и Charlie
(3, 5, '2025-01-05 22:00:00+00'); -- Bob и Diana

-- Создание сообщений
INSERT INTO message (author_id, chat_id, content, created_at) VALUES
(2, 1, 'Привет, Bob! Как дела?', '2025-01-02 20:30:00+00'),
(3, 1, 'Привет, Alice! Все хорошо, спасибо!', '2025-01-02 20:35:00+00'),
(2, 1, 'Рад слышать! Что нового?', '2025-01-02 20:40:00+00'),
(3, 1, 'Работаю над новым проектом.', '2025-01-02 20:45:00+00'),
(4, 2, 'Привет, Alice! Видел твой пост.', '2025-01-04 21:30:00+00'),
(2, 2, 'Спасибо! Понравился?', '2025-01-04 21:35:00+00'),
(4, 2, 'Да, очень интересная тема.', '2025-01-04 21:40:00+00'),
(3, 3, 'Привет, Diana!', '2025-01-05 22:30:00+00'),
(5, 3, 'Привет, Bob! Как поживаешь?', '2025-01-05 22:35:00+00'),
(3, 3, 'Отлично! А у тебя?', '2025-01-05 22:40:00+00'),
(5, 3, 'Тоже хорошо. Давай пообщаемся о технологиях.', '2025-01-05 22:45:00+00');

-- Добавление файла к сообщению
UPDATE message SET media_id = 8 WHERE id = 11;

-- Статус чтения сообщений
INSERT INTO chat_read_status (chat_id, user_id, read_at) VALUES
(1, 2, '2025-01-02 20:50:00+00'),
(1, 3, '2025-01-02 20:50:00+00'),
(2, 2, '2025-01-04 21:50:00+00'),
(2, 4, '2025-01-04 21:50:00+00'),
(3, 3, '2025-01-05 23:00:00+00'),
(3, 5, '2025-01-05 23:00:00+00');

-- Создание жалоб
INSERT INTO report (author_id, target_user_id, comment, is_reviewed, created_at) VALUES
(4, 6, 'Пользователь Eve ведет себя неадекватно в чатах', false, '2025-01-06 10:00:00+00'),
(5, 6, 'Жалоба на спам от пользователя Eve', false, '2025-01-06 11:00:00+00');

INSERT INTO report (author_id, post_id, comment, is_reviewed, created_at) VALUES
(3, 5, 'Пост содержит неподобающий контент', false, '2025-01-02 16:00:00+00');

-- Создание банов
INSERT INTO ban (banned_user_id, admin_id, report_id, start_date, end_date, reason) VALUES
(6, 1, 1, '2025-01-06 12:00:00+00', '2025-01-13 12:00:00+00', 'Нарушение правил поведения');

-- Записи в лог
INSERT INTO log (user_id, time, details) VALUES
(2, '2025-01-02 10:00:00+00', '2025-01-02 10:00:00: User registered with nick: alice'),
(3, '2025-01-03 11:00:00+00', '2025-01-03 11:00:00: User registered with nick: bob'),
(4, '2025-01-04 12:00:00+00', '2025-01-04 12:00:00: User registered with nick: charlie'),
(5, '2025-01-05 13:00:00+00', '2025-01-05 13:00:00: User registered with nick: diana'),
(6, '2025-01-06 14:00:00+00', '2025-01-06 14:00:00: User registered with nick: eve'),
(2, '2025-01-02 10:30:00+00', '2025-01-02 10:30:00: Created post 1'),
(3, '2025-01-03 11:30:00+00', '2025-01-03 11:30:00: Liked post 1'),
(1, '2025-01-06 12:00:00+00', '2025-01-06 12:00:00: Banned user 6 for report 1');

COMMIT;
