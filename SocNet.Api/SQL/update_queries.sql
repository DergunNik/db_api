-- Запросы для обновления данных (UPDATE)
-- Данный файл содержит примеры SQL запросов для изменения существующих записей

-- 1. Обновление профиля пользователя
-- Изменение ника пользователя
UPDATE "user"
SET nick = 'new_nickname'
WHERE id = 1;

-- Изменение аватара пользователя
UPDATE "user"
SET avatar_id = 5
WHERE id = 1;

-- Изменение роли пользователя на администратора
UPDATE "user"
SET is_admin = true
WHERE id = 1;

-- 2. Обновление поста
-- Редактирование текста поста
UPDATE post
SET text = 'Обновленный текст поста'
WHERE id = 1 AND author_id = 1; -- Только автор может редактировать

-- 3. Обновление медиа-файла
-- Изменение пути к файлу (например, при перемещении)
UPDATE media
SET file_path = '/uploads/new_path/avatar_123.jpg'
WHERE id = 5;

-- 4. Обновление сообщения в чате
-- Редактирование отправленного сообщения (в течение ограниченного времени)
UPDATE message
SET content = 'Исправленный текст сообщения'
WHERE id = 10 AND author_id = 1; -- Только автор может редактировать

-- 5. Обновление статуса жалобы
-- Отметка жалобы как рассмотренной
UPDATE report
SET is_reviewed = true
WHERE id = 3;

-- 6. Обновление бана
-- Продление срока бана
UPDATE ban
SET end_date = '2025-12-31 23:59:59',
    reason = 'Продленный бан за повторное нарушение'
WHERE id = 2 AND banned_user_id = 5;

-- Снятие бана (установка end_date в прошлое)
UPDATE ban
SET end_date = now() - interval '1 minute'
WHERE id = 2;

-- 7. Обновление пароля пользователя
-- При смене пароля (обычно используется хэшированный пароль)
UPDATE "user"
SET password_hash = '$2a$11$new.hash.here'
WHERE id = 1;

-- 8. Массовое обновление
-- Отметка всех жалоб старше 30 дней как рассмотренных
UPDATE report
SET is_reviewed = true
WHERE created_at < now() - interval '30 days' AND is_reviewed = false;
