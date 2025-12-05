using System.Data;
using System.Security.Claims;
using Dapper;
using Npgsql;
using SocNet.Api.Entities;

namespace SocNet.Api.Api;

public static class PostApi
{
    public static IEndpointRouteBuilder MapPostEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var loggedApi = new PostApiLogged(config);

        var group = routes.MapGroup("/posts")
            .RequireAuthorization()
            .WithTags("Posts");

        group.MapPost("/", async (CreatePostRequest req, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (await loggedApi.IsUserBanned(userId))
                return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var sql = @"INSERT INTO post (text, author_id, answer_to_id)
                       VALUES (@text, @authorId, @answerToId)
                       RETURNING *";

            var post = await db.QueryFirstAsync<Post>(sql, new
            {
                text = req.text,
                authorId = userId,
                answerToId = req.answer_to_id
            });

            await loggedApi.LogAction(userId, $"Created post {post.id}");

            return Results.Created($"/posts/{post.id}", post);
        });

        group.MapGet("/{postId:long}", async (long postId) =>
        {
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var post = await db.QueryFirstOrDefaultAsync<PostDetails>(
                @"SELECT p.id, p.text, p.answer_to_id, p.created_at,
                         u.nick as author_nick, u.id as author_id,
                         m.file_path as author_avatar,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count,
                         (SELECT COUNT(*) FROM post WHERE answer_to_id = p.id) as replies_count
                  FROM post p
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE p.id = @postId",
                new { postId });

            if (post is null)
                return Results.NotFound();

            var replies = await db.QueryAsync<PostSummary>(
                @"SELECT p.id, p.text, p.created_at,
                         u.nick as author_nick,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count
                  FROM post p
                  JOIN ""user"" u ON p.author_id = u.id
                  WHERE p.answer_to_id = @postId
                  ORDER BY p.created_at ASC",
                new { postId });

            return Results.Ok(new { post, replies });
        });

        group.MapGet("/feed", async (ClaimsPrincipal user, int page = 1, int pageSize = 20) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var posts = await db.QueryAsync<PostDetails>(
                @"SELECT DISTINCT p.id, p.text, p.answer_to_id, p.created_at,
                         u.nick as author_nick, u.id as author_id,
                         m.file_path as author_avatar,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count,
                         (SELECT COUNT(*) FROM post WHERE answer_to_id = p.id) as replies_count
                  FROM post p
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media m ON u.avatar_id = m.id
                  JOIN subscription s ON p.author_id = s.user_to_id
                  WHERE s.user_from_id = @userId
                  ORDER BY p.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            return Results.Ok(posts);
        });

        group.MapGet("/user/{userId:long}", async (long userId, int page = 1, int pageSize = 20) =>
        {
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            var posts = await db.QueryAsync<PostDetails>(
                @"SELECT p.id, p.text, p.answer_to_id, p.created_at,
                         u.nick as author_nick, u.id as author_id,
                         m.file_path as author_avatar,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count,
                         (SELECT COUNT(*) FROM post WHERE answer_to_id = p.id) as replies_count
                  FROM post p
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE p.author_id = @userId
                  ORDER BY p.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            return Results.Ok(posts);
        });

        group.MapPut("/{postId:long}", async (long postId, UpdatePostRequest req, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var authorId = await db.QueryFirstOrDefaultAsync<long?>(
                "SELECT author_id FROM post WHERE id = @postId",
                new { postId });

            if (!authorId.HasValue)
                return Results.NotFound();

            if (authorId.Value != userId)
                return Results.BadRequest("Only author can edit post");

            await db.ExecuteAsync(
                "UPDATE post SET text = @text WHERE id = @postId",
                new { postId, text = req.text });

            await loggedApi.LogAction(userId, $"Edited post {postId}");

            return Results.Ok();
        });

        group.MapDelete("/{postId:long}", async (long postId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var authorId = await db.QueryFirstOrDefaultAsync<long?>(
                "SELECT author_id FROM post WHERE id = @postId",
                new { postId });

            if (!authorId.HasValue)
                return Results.NotFound();

            if (authorId.Value != userId)
                return Results.BadRequest("Only author can delete post");

            await db.ExecuteAsync("DELETE FROM post WHERE id = @postId", new { postId });

            await loggedApi.LogAction(userId, $"Deleted post {postId}");

            return Results.Ok();
        });

        group.MapPost("/{postId:long}/like", async (long postId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (await loggedApi.IsUserBanned(userId))
                return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var postExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM post WHERE id = @postId)",
                new { postId });

            if (!postExists)
                return Results.NotFound("Post not found");

            await db.ExecuteAsync(
                @"INSERT INTO ""like"" (post_id, user_id) VALUES (@postId, @userId)
                  ON CONFLICT (post_id, user_id) DO NOTHING",
                new { postId, userId });

            await loggedApi.LogAction(userId, $"Liked post {postId}");

            return Results.Ok();
        });

        group.MapDelete("/{postId:long}/like", async (long postId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync(
                @"DELETE FROM ""like"" WHERE post_id = @postId AND user_id = @userId",
                new { postId, userId });

            await loggedApi.LogAction(userId, $"Unliked post {postId}");

            return Results.Ok();
        });

        group.MapPost("/{postId:long}/favorite", async (long postId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var postExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM post WHERE id = @postId)",
                new { postId });

            if (!postExists)
                return Results.NotFound("Post not found");

            await db.ExecuteAsync(
                @"INSERT INTO favorite (user_id, post_id) VALUES (@userId, @postId)
                  ON CONFLICT (user_id, post_id) DO NOTHING",
                new { userId, postId });

            await loggedApi.LogAction(userId, $"Added post {postId} to favorites");

            return Results.Ok();
        });

        group.MapDelete("/{postId:long}/favorite", async (long postId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync(
                @"DELETE FROM favorite WHERE user_id = @userId AND post_id = @postId",
                new { userId, postId });

            await loggedApi.LogAction(userId, $"Removed post {postId} from favorites");

            return Results.Ok();
        });

        group.MapGet("/favorites", async (ClaimsPrincipal user, int page = 1, int pageSize = 20) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var posts = await db.QueryAsync<PostDetails>(
                @"SELECT p.id, p.text, p.answer_to_id, p.created_at,
                         u.nick as author_nick, u.id as author_id,
                         m.file_path as author_avatar,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count,
                         (SELECT COUNT(*) FROM post WHERE answer_to_id = p.id) as replies_count,
                         f.created_at as favorited_at
                  FROM favorite f
                  JOIN post p ON f.post_id = p.id
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE f.user_id = @userId
                  ORDER BY f.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            return Results.Ok(posts);
        });

        return routes;
    }

    private class PostApiLogged : LoggedApi
    {
        public string ConnectionString { get; }

        public PostApiLogged(IConfiguration config) : base(config)
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

    public class CreatePostRequest
    {
        public string text { get; set; } = string.Empty;
        public long? answer_to_id { get; set; }
    }

    public class UpdatePostRequest
    {
        public string text { get; set; } = string.Empty;
    }

    public class PostDetails
    {
        public long id { get; set; }
        public string? text { get; set; }
        public long? answer_to_id { get; set; }
        public DateTime created_at { get; set; }
        public string author_nick { get; set; } = string.Empty;
        public long author_id { get; set; }
        public string? author_avatar { get; set; }
        public int likes_count { get; set; }
        public int replies_count { get; set; }
    }

    public class PostSummary
    {
        public long id { get; set; }
        public string? text { get; set; }
        public DateTime created_at { get; set; }
        public string author_nick { get; set; } = string.Empty;
        public int likes_count { get; set; }
    }
}
