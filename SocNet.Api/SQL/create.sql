BEGIN;

DROP TABLE IF EXISTS log CASCADE;
DROP TABLE IF EXISTS ban CASCADE;
DROP TABLE IF EXISTS report CASCADE;
DROP TABLE IF EXISTS message CASCADE;
DROP TABLE IF EXISTS chat CASCADE;
DROP TABLE IF EXISTS subscription CASCADE;
DROP TABLE IF EXISTS favorite CASCADE;
DROP TABLE IF EXISTS "like" CASCADE;
DROP TABLE IF EXISTS post CASCADE;
DROP TABLE IF EXISTS media CASCADE;
DROP TABLE IF EXISTS "user" CASCADE;

CREATE TABLE "user" (
                        id BIGSERIAL PRIMARY KEY,
                        nick VARCHAR(100) NOT NULL UNIQUE,
                        password_hash TEXT NOT NULL,
                        avatar_id BIGINT,
                        is_admin BOOLEAN NOT NULL,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT now()
);

CREATE TABLE media (
                       id BIGSERIAL PRIMARY KEY,
                       file_path TEXT NOT NULL,
);

CREATE TABLE post (
                      id BIGSERIAL PRIMARY KEY,
                      text TEXT,
                      author_id BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                      answer_to_id BIGINT REFERENCES post(id) ON DELETE SET NULL,
                      created_at TIMESTAMP WITH TIME ZONE DEFAULT now()
);

CREATE TABLE "like" (
                        post_id BIGINT NOT NULL REFERENCES post(id) ON DELETE CASCADE,
                        user_id BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
                        PRIMARY KEY (post_id, user_id)
);

CREATE TABLE favorite (
                          user_id BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                          post_id BIGINT NOT NULL REFERENCES post(id) ON DELETE CASCADE,
                          created_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
                          PRIMARY KEY (user_id, post_id)
);

CREATE TABLE subscription (
                              user_from_id BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                              user_to_id   BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                              created_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
                              PRIMARY KEY (user_from_id, user_to_id),
                              CHECK (user_from_id <> user_to_id)
);

CREATE TABLE chat (
                      id BIGSERIAL PRIMARY KEY,
                      first_user_id BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                      second_user_id BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                      created_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
                      CHECK (first_user_id < second_user_id),
                      UNIQUE (first_user_id, second_user_id)
);

CREATE TABLE message (
                         id BIGSERIAL PRIMARY KEY,
                         author_id BIGINT REFERENCES "user"(id) ON DELETE SET NULL,
                         chat_id BIGINT NOT NULL REFERENCES chat(id) ON DELETE CASCADE,
                         created_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
                         content TEXT NOT NULL,
                         file_path TEXT
);

CREATE TABLE chat_read_status (
                               chat_id BIGINT NOT NULL REFERENCES chat(id) ON DELETE CASCADE,
                               user_id BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                               read_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
                               PRIMARY KEY (chat_id, user_id)
);

CREATE TABLE report (
                        id BIGSERIAL PRIMARY KEY,
                        author_id BIGINT REFERENCES "user"(id) ON DELETE SET NULL,
                        target_user_id BIGINT REFERENCES "user"(id) ON DELETE SET NULL,
                        post_id BIGINT REFERENCES post(id) ON DELETE SET NULL,
                        comment TEXT,
                        is_reviewed BOOLEAN DEFAULT FALSE,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT now()
);

CREATE TABLE ban (
                     id BIGSERIAL PRIMARY KEY,
                     banned_user_id BIGINT NOT NULL REFERENCES "user"(id) ON DELETE CASCADE,
                     admin_id BIGINT REFERENCES "user"(id) ON DELETE SET NULL,
                     report_id BIGINT REFERENCES report(id) ON DELETE SET NULL,
                     start_date TIMESTAMP WITH TIME ZONE DEFAULT now(),
                     end_date TIMESTAMP WITH TIME ZONE,
                     reason TEXT
);

CREATE TABLE log (
                     id BIGSERIAL PRIMARY KEY,
                     time TIMESTAMP WITH TIME ZONE DEFAULT now(),
                     user_id BIGINT REFERENCES "user"(id) ON DELETE SET NULL,
                     details TEXT
);

-- Индексы для оптимизации запросов

-- Индексы для таблицы user
CREATE INDEX idx_user_nick ON "user"(nick); -- Поиск пользователей по нику

-- Индексы для таблицы post
CREATE INDEX idx_post_author_id ON post(author_id); -- Посты пользователя
CREATE INDEX idx_post_answer_to_id ON post(answer_to_id); -- Ответы на пост
CREATE INDEX idx_post_created_at ON post(created_at); -- Сортировка постов по времени

-- Индексы для таблицы favorite
CREATE INDEX idx_favorite_post_id ON favorite(post_id); -- Кто добавил пост в избранное

-- Индексы для таблицы subscription
CREATE INDEX idx_subscription_user_to_id ON subscription(user_to_id); -- Подписчики пользователя

-- Индексы для таблицы chat
CREATE INDEX idx_chat_first_user_id ON chat(first_user_id); -- Чаты первого пользователя
CREATE INDEX idx_chat_second_user_id ON chat(second_user_id); -- Чаты второго пользователя

-- Индексы для таблицы message
CREATE INDEX idx_message_chat_id ON message(chat_id); -- Сообщения в чате
CREATE INDEX idx_message_author_id ON message(author_id); -- Сообщения пользователя
CREATE INDEX idx_message_created_at ON message(created_at); -- Сортировка сообщений по времени

-- Индексы для таблицы chat_read_status
CREATE INDEX idx_chat_read_status_user_id ON chat_read_status(user_id); -- Чаты пользователя со статусом чтения

-- Индексы для таблицы ban
CREATE INDEX idx_ban_banned_user_id ON ban(banned_user_id); -- Баны пользователя
CREATE INDEX idx_ban_end_date ON ban(end_date); -- Истекшие баны

COMMIT;
