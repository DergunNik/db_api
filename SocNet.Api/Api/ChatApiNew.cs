using System.Data;
using System.Security.Claims;
using Dapper;
using Npgsql;

namespace SocNet.Api.Api;

public static class ChatApi
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var loggedApi = new ChatApiLogged(config);

        var group = routes.MapGroup("/chats")
            .RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, int page = 1, int pageSize = 20) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
                  ORDER BY last_message_time DESC NULLS LAST
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            return Results.Ok(chats);
        });

        group.MapPut("/with/{targetUserId:long}", async (long targetUserId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (userId == targetUserId)
                return Results.BadRequest("Cannot create chat with yourself");

            if (await loggedApi.IsUserBanned(userId))
                return Results.Forbidden("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var targetExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM \"user\" WHERE id = @targetUserId)",
                new { targetUserId });

            if (!targetExists)
                return Results.NotFound("Target user not found");

            var existingChat = await db.QueryFirstOrDefaultAsync<long?>(
                @"SELECT id FROM chat
                  WHERE (first_user_id = @userId AND second_user_id = @targetUserId)
                     OR (first_user_id = @targetUserId AND second_user_id = @userId)",
                new { userId, targetUserId });

            if (existingChat.HasValue)
                return Results.Ok(new { chatId = existingChat.Value });

            var chatId = await db.QueryFirstAsync<long>(
                @"INSERT INTO chat (first_user_id, second_user_id)
                  VALUES (@firstUserId, @secondUserId)
                  RETURNING id",
                new
                {
                    firstUserId = Math.Min(userId, targetUserId),
                    secondUserId = Math.Max(userId, targetUserId)
                });

            await loggedApi.LogAction(userId, $"Created chat with user {targetUserId}");

            return Results.Created($"/chats/{chatId}", new { chatId });
        });

        group.MapGet("/{chatId:long}/messages", async (long chatId, ClaimsPrincipal user, int page = 1, int pageSize = 50) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var hasAccess = await db.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS(
                    SELECT 1 FROM chat
                    WHERE id = @chatId AND (first_user_id = @userId OR second_user_id = @userId)
                )",
                new { chatId, userId });

            if (!hasAccess)
                return Results.Forbidden("No access to this chat");

            var messages = await db.QueryAsync<MessageDetails>(
                @"SELECT m.id, m.content, m.created_at,
                         u.nick as author_nick, u.id as author_id,
                         m.file_path as attached_file
                  FROM message m
                  JOIN ""user"" u ON m.author_id = u.id
                  WHERE m.chat_id = @chatId
                  ORDER BY m.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { chatId, pageSize, offset = (page - 1) * pageSize });

            await db.ExecuteAsync(
                @"INSERT INTO chat_read_status (chat_id, user_id, read_at)
                  VALUES (@chatId, @userId, now())
                  ON CONFLICT (chat_id, user_id) DO UPDATE SET read_at = now()",
                new { chatId, userId });

            return Results.Ok(messages);
        });

        group.MapPost("/{chatId:long}/messages", async (long chatId, SendMessageRequest req, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (await loggedApi.IsUserBanned(userId))
                return Results.Forbidden("User is banned");

            if (string.IsNullOrWhiteSpace(req.Content))
                return Results.BadRequest("Message content cannot be empty");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var hasAccess = await db.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS(
                    SELECT 1 FROM chat
                    WHERE id = @chatId AND (first_user_id = @userId OR second_user_id = @userId)
                )",
                new { chatId, userId });

            if (!hasAccess)
                return Results.Forbidden("No access to this chat");

            var message = await db.QueryFirstAsync<MessageDetails>(
                @"INSERT INTO message (author_id, chat_id, content, file_path)
                  VALUES (@authorId, @chatId, @content, @filePath)
                  RETURNING id, content, created_at",
                new
                {
                    authorId = userId,
                    chatId,
                    content = req.Content,
                    filePath = req.FilePath
                });

            var authorInfo = await db.QueryFirstAsync<dynamic>(
                @"SELECT nick, id FROM ""user"" WHERE id = @userId",
                new { userId });

            message = message with { AuthorNick = authorInfo.nick, AuthorId = authorInfo.id };

            await loggedApi.LogAction(userId, $"Sent message in chat {chatId}");

            return Results.Created($"/chats/{chatId}/messages/{message.Id}", message);
        });

        group.MapPut("/{chatId:long}/messages/{messageId:long}", async (long chatId, long messageId, UpdateMessageRequest req, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var messageInfo = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT author_id FROM message WHERE id = @messageId AND chat_id = @chatId",
                new { messageId, chatId });

            if (messageInfo == null)
                return Results.NotFound("Message not found");

            if ((long)messageInfo.author_id != userId)
                return Results.Forbidden("Only author can edit message");

            await db.ExecuteAsync(
                @"UPDATE message SET content = @content, file_path = @filePath
                  WHERE id = @messageId",
                new { messageId, content = req.Content, filePath = req.FilePath });

            await loggedApi.LogAction(userId, $"Edited message {messageId} in chat {chatId}");

            return Results.Ok();
        });

        group.MapDelete("/{chatId:long}/messages/{messageId:long}", async (long chatId, long messageId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var messageInfo = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT author_id FROM message WHERE id = @messageId AND chat_id = @chatId",
                new { messageId, chatId });

            if (messageInfo == null)
                return Results.NotFound("Message not found");

            if ((long)messageInfo.author_id != userId)
                return Results.Forbidden("Only author can delete message");

            await db.ExecuteAsync(
                @"DELETE FROM message WHERE id = @messageId",
                new { messageId });

            await loggedApi.LogAction(userId, $"Deleted message {messageId} in chat {chatId}");

            return Results.Ok();
        });

        return routes;
    }

    private class ChatApiLogged : LoggedApi
    {
        public string ConnectionString { get; }

        public ChatApiLogged(IConfiguration config) : base(config)
        {
            ConnectionString = config.GetConnectionString("DefaultConnection")
                              ?? throw new Exception("Connection string not found");
        }

        public async Task<bool> IsUserBanned(long userId)
        {
            using IDbConnection db = new NpgsqlConnection(ConnectionString);
            var isBanned = await db.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS(
                    SELECT 1 FROM ban
                    WHERE banned_user_id = @userId
                      AND (end_date IS NULL OR end_date > now())
                  )",
                new { userId });
            return isBanned;
        }
    }

    public record ChatSummary(
        long Id,
        string ChatWithNick,
        long ChatWithId,
        string? ChatWithAvatar,
        DateTime ChatCreatedAt,
        string? LastMessage,
        DateTime? LastMessageTime,
        int UnreadCount
    );

    public record MessageDetails(
        long Id,
        string Content,
        DateTime CreatedAt,
        string AuthorNick,
        long AuthorId,
        string? AttachedFile
    );

    public record SendMessageRequest(string Content, string? FilePath);

    public record UpdateMessageRequest(string Content, string? FilePath);
}
