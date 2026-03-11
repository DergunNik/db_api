using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using SocNet.Api.Mongo; 

namespace SocNet.Api.Api;

public static class ChatApi
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var group = routes.MapGroup("/chats")
            .RequireAuthorization()
            .WithTags("Chat");

        group.MapGet("/", async (ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ChatApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"chats:{userId}:p:1";

            var cachedChats = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedChats))
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<ChatSummary>>(cachedChats));

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            var chats = await db.QueryAsync<ChatSummary>(
                @"SELECT c.id,
                         CASE WHEN c.first_user_id = @userId THEN u2.nick ELSE u1.nick END as chat_with_nick,
                         CASE WHEN c.first_user_id = @userId THEN u2.id ELSE u1.id END as chat_with_id,
                         CASE WHEN c.first_user_id = @userId THEN m2.file_path ELSE m1.file_path END as chat_with_avatar,
                         c.created_at as chat_created_at,
                         (SELECT content FROM message WHERE chat_id = c.id ORDER BY created_at DESC LIMIT 1) as last_message,
                         (SELECT created_at FROM message WHERE chat_id = c.id ORDER BY created_at DESC LIMIT 1) as last_message_time,
                         (SELECT COUNT(*) FROM message WHERE chat_id = c.id AND author_id != @userId AND created_at > COALESCE(
                             (SELECT read_at FROM chat_read_status WHERE chat_id = c.id AND user_id = @userId), '1970-01-01'
                         )) as unread_count
                  FROM chat c
                  JOIN ""user"" u1 ON c.first_user_id = u1.id
                  JOIN ""user"" u2 ON c.second_user_id = u2.id
                  LEFT JOIN media m1 ON u1.avatar_id = m1.id
                  LEFT JOIN media m2 ON u2.avatar_id = m2.id
                  WHERE c.first_user_id = @userId OR c.second_user_id = @userId
                  ORDER BY last_message_time DESC NULLS LAST",
                new { userId });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(chats), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

            return Results.Ok(chats);
        });

        group.MapPut("/with/{targetUserId:long}", async (long targetUserId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ChatApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            if (userId == targetUserId) return Results.BadRequest("Cannot create chat with yourself");
            if (await loggedApi.IsUserBanned(userId)) return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            var existingChat = await db.QueryFirstOrDefaultAsync<long?>(
                @"SELECT id FROM chat
                  WHERE (first_user_id = @userId AND second_user_id = @targetUserId)
                     OR (first_user_id = @targetUserId AND second_user_id = @userId)",
                new { userId, targetUserId });

            if (existingChat.HasValue) return Results.Ok(new { chatId = existingChat.Value });

            var chatId = await db.QueryFirstAsync<long>(
                @"INSERT INTO chat (first_user_id, second_user_id)
                  VALUES (@firstUserId, @secondUserId)
                  RETURNING id",
                new { firstUserId = Math.Min(userId, targetUserId), secondUserId = Math.Max(userId, targetUserId) });

            await cache.RemoveAsync($"chats:{userId}:p:1");
            await cache.RemoveAsync($"chats:{targetUserId}:p:1");

            await loggedApi.LogAction(userId, $"Created chat with user {targetUserId}");
            return Results.Created($"/chats/{chatId}", new { chatId });
        });

        group.MapGet("/{chatId:long}/messages", async (long chatId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ChatApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"messages:{chatId}";

            var cachedMsgs = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedMsgs))
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<MessageDetails>>(cachedMsgs));

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var messages = await db.QueryAsync<MessageDetails>(
                @"SELECT m.id, m.content, m.created_at, u.nick as author_nick, u.id as author_id, med.file_path as attached_file
                  FROM message m
                  JOIN ""user"" u ON m.author_id = u.id
                  LEFT JOIN media med ON m.media_id = med.id
                  WHERE m.chat_id = @chatId
                  ORDER BY m.created_at DESC",
                new { chatId });

            await db.ExecuteAsync(
                @"INSERT INTO chat_read_status (chat_id, user_id, read_at)
                  VALUES (@chatId, @userId, now())
                  ON CONFLICT (chat_id, user_id) DO UPDATE SET read_at = now()",
                new { chatId, userId });

            await cache.RemoveAsync($"chats:{userId}:p:1");
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(messages), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Results.Ok(messages);
        });

        group.MapPost("/{chatId:long}/messages", async (long chatId, SendMessageRequest req, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ChatApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            if (await loggedApi.IsUserBanned(userId)) return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            var chat = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT first_user_id, second_user_id FROM chat WHERE id = @chatId", new { chatId });

            var message = await db.QueryFirstAsync<MessageDetails>(
                @"WITH inserted AS (
                    INSERT INTO message (author_id, chat_id, content, media_id)
                    VALUES (@authorId, @chatId, @content, @mediaId)
                    RETURNING *
                  )
                  SELECT i.id, i.content, i.created_at, u.nick as author_nick, u.id as author_id, m.file_path as attached_file
                  FROM inserted i
                  JOIN ""user"" u ON i.author_id = u.id
                  LEFT JOIN media m ON i.media_id = m.id",
                new { authorId = userId, chatId, content = req.content, mediaId = req.media_id });

            await cache.RemoveAsync($"messages:{chatId}");
            await cache.RemoveAsync($"chats:{(long)chat.first_user_id}:p:1");
            await cache.RemoveAsync($"chats:{(long)chat.second_user_id}:p:1");

            await loggedApi.LogAction(userId, $"Sent message in chat {chatId}");
            return Results.Created($"/chats/{chatId}/messages/{message.id}", message);
        });

        group.MapPut("/{chatId:long}/messages/{messageId:long}", async (long chatId, long messageId, UpdateMessageRequest req, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ChatApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync(
                @"UPDATE message SET content = @content, media_id = @mediaId WHERE id = @messageId",
                new { messageId, content = req.content, mediaId = req.media_id });

            await cache.RemoveAsync($"messages:{chatId}");
            return Results.Ok();
        });

        group.MapDelete("/{chatId:long}/messages/{messageId:long}", async (long chatId, long messageId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ChatApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync("DELETE FROM message WHERE id = @messageId", new { messageId });

            await cache.RemoveAsync($"messages:{chatId}");
            await cache.RemoveAsync($"chats:{userId}:p:1");
            return Results.Ok();
        });

        return routes;
    }

    private class ChatApiLogged : LoggedApi
    {
        public ChatApiLogged(IConfiguration config, IDistributedCache cache, MongoLogService logService) 
            : base(config, cache, logService) { }
    }

    public class ChatSummary
    {
        public long id { get; set; }
        public string chat_with_nick { get; set; } = string.Empty;
        public long chat_with_id { get; set; }
        public string? chat_with_avatar { get; set; }
        public DateTime chat_created_at { get; set; }
        public string? last_message { get; set; }
        public DateTime? last_message_time { get; set; }
        public int unread_count { get; set; }
    }

    public class MessageDetails
    {
        public long id { get; set; }
        public string content { get; set; } = string.Empty;
        public DateTime created_at { get; set; }
        public string author_nick { get; set; } = string.Empty;
        public long author_id { get; set; }
        public string? attached_file { get; set; } 
    }

    public class SendMessageRequest { public string content { get; set; } = string.Empty; public long? media_id { get; set; } }
    public class UpdateMessageRequest { public string content { get; set; } = string.Empty; public long? media_id { get; set; } }
}