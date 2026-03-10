using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using SocNet.Api.Entities;

namespace SocNet.Api.Api;

public static class PostApi
{
    public static IEndpointRouteBuilder MapPostEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var group = routes.MapGroup("/posts")
            .RequireAuthorization()
            .WithTags("Posts");

        group.MapPost("/", async (CreatePostRequest req, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (await loggedApi.IsUserBanned(userId))
                return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var sql = @"INSERT INTO post (text, author_id, answer_to_id, media_id)
                       VALUES (@text, @authorId, @answerToId, @mediaId)
                       RETURNING *";

            var post = await db.QueryFirstAsync<Post>(sql, new
            {
                text = req.text,
                authorId = userId,
                answerToId = req.answer_to_id,
                mediaId = req.media_id
            });

            await cache.RemoveAsync($"user_posts:{userId}:p:1");
            await cache.RemoveAsync($"feed:{userId}:p:1");
            if (req.answer_to_id.HasValue)
                await cache.RemoveAsync($"post:{req.answer_to_id.Value}");

            await loggedApi.LogAction(userId, $"Created post {post.id}");

            return Results.Created($"/posts/{post.id}", post);
        });

        group.MapGet("/{postId:long}", async (long postId, IDistributedCache cache, IConfiguration cfg) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            string cacheKey = $"post:{postId}";
            
            var cachedPost = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedPost))
                return Results.Ok(JsonSerializer.Deserialize<object>(cachedPost));

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var post = await db.QueryFirstOrDefaultAsync<PostDetails>(
                @"SELECT p.id, p.text, p.answer_to_id, p.created_at,
                         u.nick as author_nick, u.id as author_id,
                         am.file_path as author_avatar,
                         pm.file_path as post_media,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count,
                         (SELECT COUNT(*) FROM post WHERE answer_to_id = p.id) as replies_count
                  FROM post p
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media am ON u.avatar_id = am.id
                  LEFT JOIN media pm ON p.media_id = pm.id
                  WHERE p.id = @postId",
                new { postId });

            if (post is null) return Results.NotFound();

            var replies = await db.QueryAsync<PostSummary>(
                @"SELECT p.id, p.text, p.created_at,
                         u.nick as author_nick,
                         pm.file_path as post_media,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count
                  FROM post p
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media pm ON p.media_id = pm.id
                  WHERE p.answer_to_id = @postId
                  ORDER BY p.created_at ASC",
                new { postId });

            var result = new { post, replies };
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Results.Ok(result);
        });

        group.MapGet("/feed", async (ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, int page = 1, int pageSize = 20) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"feed:{userId}:p:{page}";

            var cachedFeed = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedFeed))
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<PostDetails>>(cachedFeed));

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var posts = await db.QueryAsync<PostDetails>(
                @"SELECT DISTINCT p.id, p.text, p.answer_to_id, p.created_at,
                         u.nick as author_nick, u.id as author_id,
                         am.file_path as author_avatar,
                         pm.file_path as post_media,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count,
                         (SELECT COUNT(*) FROM post WHERE answer_to_id = p.id) as replies_count
                  FROM post p
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media am ON u.avatar_id = am.id
                  LEFT JOIN media pm ON p.media_id = pm.id
                  JOIN subscription s ON p.author_id = s.user_to_id
                  WHERE s.user_from_id = @userId
                  ORDER BY p.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(posts), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

            return Results.Ok(posts);
        });

        group.MapGet("/user/{userId:long}", async (long userId, IDistributedCache cache, IConfiguration cfg, int page = 1, int pageSize = 20) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            string cacheKey = $"user_posts:{userId}:p:{page}";

            var cachedUserPosts = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedUserPosts))
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<PostDetails>>(cachedUserPosts));

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            var posts = await db.QueryAsync<PostDetails>(
                @"SELECT p.id, p.text, p.answer_to_id, p.created_at,
                         u.nick as author_nick, u.id as author_id,
                         am.file_path as author_avatar,
                         pm.file_path as post_media,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count,
                         (SELECT COUNT(*) FROM post WHERE answer_to_id = p.id) as replies_count
                  FROM post p
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media am ON u.avatar_id = am.id
                  LEFT JOIN media pm ON p.media_id = pm.id
                  WHERE p.author_id = @userId
                  ORDER BY p.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(posts), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Results.Ok(posts);
        });

        group.MapPut("/{postId:long}", async (long postId, UpdatePostRequest req, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var authorId = await db.QueryFirstOrDefaultAsync<long?>(
                "SELECT author_id FROM post WHERE id = @postId",
                new { postId });

            if (!authorId.HasValue) return Results.NotFound();
            if (authorId.Value != userId) return Results.BadRequest("Only author can edit post");

            await db.ExecuteAsync(
                "UPDATE post SET text = @text, media_id = @mediaId WHERE id = @postId",
                new { postId, text = req.text, mediaId = req.media_id });

            await cache.RemoveAsync($"post:{postId}");
            await cache.RemoveAsync($"user_posts:{userId}:p:1");

            await loggedApi.LogAction(userId, $"Edited post {postId}");
            return Results.Ok();
        });

        group.MapDelete("/{postId:long}", async (long postId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var postData = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT author_id, answer_to_id FROM post WHERE id = @postId",
                new { postId });

            if (postData == null) return Results.NotFound();
            if ((long)postData.author_id != userId) return Results.BadRequest("Only author can delete post");

            await db.ExecuteAsync("DELETE FROM post WHERE id = @postId", new { postId });

            await cache.RemoveAsync($"post:{postId}");
            await cache.RemoveAsync($"user_posts:{userId}:p:1");
            if (postData.answer_to_id != null)
                await cache.RemoveAsync($"post:{(long)postData.answer_to_id}");

            await loggedApi.LogAction(userId, $"Deleted post {postId}");
            return Results.Ok();
        });

        group.MapPost("/{postId:long}/like", async (long postId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (await loggedApi.IsUserBanned(userId)) return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var postExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM post WHERE id = @postId)",
                new { postId });

            if (!postExists) return Results.NotFound("Post not found");

            await db.ExecuteAsync(
                @"INSERT INTO ""like"" (post_id, user_id) VALUES (@postId, @userId)
                  ON CONFLICT (post_id, user_id) DO NOTHING",
                new { postId, userId });

            await cache.RemoveAsync($"post:{postId}");
            await loggedApi.LogAction(userId, $"Liked post {postId}");

            return Results.Ok();
        });

        group.MapDelete("/{postId:long}/like", async (long postId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync(
                @"DELETE FROM ""like"" WHERE post_id = @postId AND user_id = @userId",
                new { postId, userId });

            await cache.RemoveAsync($"post:{postId}");
            await loggedApi.LogAction(userId, $"Unliked post {postId}");

            return Results.Ok();
        });

        group.MapPost("/{postId:long}/favorite", async (long postId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var postExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM post WHERE id = @postId)",
                new { postId });

            if (!postExists) return Results.NotFound("Post not found");

            await db.ExecuteAsync(
                @"INSERT INTO favorite (user_id, post_id) VALUES (@userId, @postId)
                  ON CONFLICT (user_id, post_id) DO NOTHING",
                new { userId, postId });

            await cache.RemoveAsync($"favorites:{userId}:p:1");
            await loggedApi.LogAction(userId, $"Added post {postId} to favorites");

            return Results.Ok();
        });

        group.MapDelete("/{postId:long}/favorite", async (long postId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync(
                @"DELETE FROM favorite WHERE user_id = @userId AND post_id = @postId",
                new { userId, postId });

            await cache.RemoveAsync($"favorites:{userId}:p:1");
            await loggedApi.LogAction(userId, $"Removed post {postId} from favorites");

            return Results.Ok();
        });

        group.MapGet("/favorites", async (ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, int page = 1, int pageSize = 20) =>
        {
            var loggedApi = new PostApiLogged(cfg, cache);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"favorites:{userId}:p:{page}";

            var cachedFavs = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedFavs))
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<PostDetails>>(cachedFavs));

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var posts = await db.QueryAsync<PostDetails>(
                @"SELECT p.id, p.text, p.answer_to_id, p.created_at,
                         u.nick as author_nick, u.id as author_id,
                         am.file_path as author_avatar,
                         pm.file_path as post_media,
                         (SELECT COUNT(*) FROM ""like"" WHERE post_id = p.id) as likes_count,
                         (SELECT COUNT(*) FROM post WHERE answer_to_id = p.id) as replies_count,
                         f.created_at as favorited_at
                  FROM favorite f
                  JOIN post p ON f.post_id = p.id
                  JOIN ""user"" u ON p.author_id = u.id
                  LEFT JOIN media am ON u.avatar_id = am.id
                  LEFT JOIN media pm ON p.media_id = pm.id
                  WHERE f.user_id = @userId
                  ORDER BY f.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(posts), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Results.Ok(posts);
        });

        return routes;
    }

    private class PostApiLogged : LoggedApi
    {
        public PostApiLogged(IConfiguration config, IDistributedCache cache) : base(config, cache) { }
    }

    public class CreatePostRequest
    {
        public string text { get; set; } = string.Empty;
        public long? answer_to_id { get; set; }
        public long? media_id { get; set; }
    }

    public class UpdatePostRequest
    {
        public string text { get; set; } = string.Empty;
        public long? media_id { get; set; }
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
        public string? post_media { get; set; }
        public int likes_count { get; set; }
        public int replies_count { get; set; }
    }

    public class PostSummary
    {
        public long id { get; set; }
        public string? text { get; set; }
        public DateTime created_at { get; set; }
        public string author_nick { get; set; } = string.Empty;
        public string? post_media { get; set; }
        public int likes_count { get; set; }
    }
}