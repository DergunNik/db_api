using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using SocNet.Api.Mongo;

namespace SocNet.Api.Api;

public static class UserApi
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var group = routes.MapGroup("/users")
            .RequireAuthorization()
            .WithTags("Users");

        group.MapGet("/profile", async (ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new UserApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"user:profile:{userId}";

            var cachedProfile = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedProfile))
            {
                return Results.Ok(JsonSerializer.Deserialize<UserProfile>(cachedProfile));
            }

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(userId, "SELECT user profile details");
            var profile = await db.QueryFirstOrDefaultAsync<UserProfile>(
                @"SELECT u.id, u.nick, u.is_admin, u.created_at,
                         m.file_path as avatar_path,
                         (SELECT COUNT(*) FROM subscription WHERE user_to_id = u.id) as followers_count,
                         (SELECT COUNT(*) FROM subscription WHERE user_from_id = u.id) as following_count,
                         (SELECT COUNT(*) FROM post WHERE author_id = u.id) as posts_count
                  FROM ""user"" u
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE u.id = @userId",
                new { userId });

            if (profile is null) return Results.NotFound();

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(profile), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            return Results.Ok(profile);
        });

        group.MapGet("/search", async (string query, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService, int limit = 20) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Results.BadRequest("Search query must be at least 2 characters");

            var loggedApi = new UserApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"user:search:{query.ToLower()}:{limit}";

            var cachedSearch = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedSearch))
            {
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<UserSearchResult>>(cachedSearch));
            }

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(userId, $"SELECT user: {query}");
            var users = await db.QueryAsync<UserSearchResult>(
                @"SELECT u.id, u.nick, m.file_path as avatar_path
                  FROM ""user"" u
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE u.nick ILIKE @query
                  ORDER BY u.nick
                  LIMIT @limit",
                new { query = $"%{query}%", limit });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(users), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

            return Results.Ok(users);
        });

        group.MapPut("/profile", async (UpdateProfileRequest req, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new UserApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            if (!string.IsNullOrWhiteSpace(req.nick))
            {
                await loggedApi.LogDbQuery(userId, "SELECT for checking nick availability");
                var existing = await db.QueryFirstOrDefaultAsync<long?>(
                    @"SELECT id FROM ""user"" WHERE nick = @nick AND id != @userId",
                    new { nick = req.nick, userId });

                if (existing.HasValue) return Results.Conflict("Nick already taken");
            }

            var updateFields = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("userId", userId);

            if (!string.IsNullOrWhiteSpace(req.nick))
            {
                updateFields.Add("nick = @nick");
                parameters.Add("nick", req.nick);
            }

            if (req.avatar_id.HasValue)
            {
                updateFields.Add("avatar_id = @avatarId");
                parameters.Add("avatarId", req.avatar_id.Value);
            }

            if (updateFields.Any())
            {
                var sql = $"UPDATE \"user\" SET {string.Join(", ", updateFields)} WHERE id = @userId";
                
                await loggedApi.LogDbQuery(userId, $"UPDATE profile with id {userId}");
                await db.ExecuteAsync(sql, parameters);

                await cache.RemoveAsync($"user:profile:{userId}");
                await loggedApi.LogAction(userId, "Profile updated");
            }

            return Results.Ok();
        });

        group.MapGet("/{userId:long}/followers", async (long userId, ClaimsPrincipal currentUser, IDistributedCache cache, IConfiguration cfg, MongoLogService logService, int page = 1, int pageSize = 20) =>
        {
            var loggedApi = new UserApiLogged(cfg, cache, logService);
            var currentUserId = long.Parse(currentUser.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"user:followers:{userId}:p:{page}";

            var cachedFollowers = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedFollowers))
            {
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<UserSummary>>(cachedFollowers));
            }

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(currentUserId, $"SELECT followers for user {userId}");
            var followers = await db.QueryAsync<UserSummary>(
                @"SELECT u.id, u.nick, m.file_path as avatar_path, s.created_at as relation_date
                  FROM subscription s
                  JOIN ""user"" u ON s.user_from_id = u.id
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE s.user_to_id = @userId
                  ORDER BY s.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(followers), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Results.Ok(followers);
        });

        group.MapGet("/{userId:long}/following", async (long userId, ClaimsPrincipal currentUser, IDistributedCache cache, IConfiguration cfg, MongoLogService logService, int page = 1, int pageSize = 20) =>
        {
            var loggedApi = new UserApiLogged(cfg, cache, logService);
            var currentUserId = long.Parse(currentUser.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"user:following:{userId}:p:{page}";

            var cachedFollowing = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedFollowing))
            {
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<UserSummary>>(cachedFollowing));
            }

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(currentUserId, $"SELECT following list for user {userId}");
            var following = await db.QueryAsync<UserSummary>(
                @"SELECT u.id, u.nick, m.file_path as avatar_path, s.created_at as relation_date
                  FROM subscription s
                  JOIN ""user"" u ON s.user_to_id = u.id
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE s.user_from_id = @userId
                  ORDER BY s.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(following), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Results.Ok(following);
        });

        var adminGroup = routes.MapGroup("/admin/users")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithTags("Admin");

        adminGroup.MapGet("/", async (ClaimsPrincipal admin, IDistributedCache cache, IConfiguration cfg, MongoLogService logService, int page = 1, int pageSize = 50) =>
        {
            var loggedApi = new UserApiLogged(cfg, cache, logService);
            var adminId = long.Parse(admin.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"admin:users:p:{page}";

            var cachedUsers = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedUsers))
            {
                return Results.Ok(JsonSerializer.Deserialize<IEnumerable<UserAdminView>>(cachedUsers));
            }

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(adminId, "SELECT all users list by admin");
            var users = await db.QueryAsync<UserAdminView>(
                @"SELECT u.id, u.nick, u.is_admin, u.created_at,
                         m.file_path as avatar_path,
                         CASE WHEN b.end_date IS NULL OR b.end_date > now() THEN true ELSE false END as is_banned,
                         b.reason as ban_reason, b.end_date as ban_end_date
                  FROM ""user"" u
                  LEFT JOIN media m ON u.avatar_id = m.id
                  LEFT JOIN ban b ON u.id = b.banned_user_id AND (b.end_date IS NULL OR b.end_date > now())
                  ORDER BY u.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { pageSize, offset = (page - 1) * pageSize });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(users), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

            return Results.Ok(users);
        });

        adminGroup.MapPut("/{userId:long}/role", async (long userId, UpdateRoleRequest req, ClaimsPrincipal admin, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new UserApiLogged(cfg, cache, logService);
            var adminId = long.Parse(admin.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await loggedApi.LogDbQuery(adminId, $"UPDATE role for user {userId} by admin");
            await db.ExecuteAsync(
                "UPDATE \"user\" SET is_admin = @isAdmin WHERE id = @userId",
                new { userId, isAdmin = req.is_admin });

            await cache.RemoveAsync($"user:profile:{userId}");
            await cache.RemoveAsync("admin:users:p:1");
            await loggedApi.InvalidateUserCache(userId);

            await loggedApi.LogAction(adminId, $"Changed user {userId} role to {(req.is_admin ? "admin" : "user")}");

            return Results.Ok();
        });

        adminGroup.MapDelete("/{userId:long}", async (long userId, ClaimsPrincipal admin, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new UserApiLogged(cfg, cache, logService);
            var adminId = long.Parse(admin.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await loggedApi.LogDbQuery(adminId, $"DELETE user {userId} by admin");
            await db.ExecuteAsync("DELETE FROM \"user\" WHERE id = @userId", new { userId });

            await cache.RemoveAsync($"user:profile:{userId}");
            await cache.RemoveAsync("admin:users:p:1");
            await loggedApi.InvalidateUserCache(userId);

            await loggedApi.LogAction(adminId, $"Deleted user {userId}");

            return Results.Ok();
        });

        return routes;
    }

    private class UserApiLogged : LoggedApi
    {
        public UserApiLogged(IConfiguration config, IDistributedCache cache, MongoLogService logService) 
            : base(config, cache, logService) { }
    }

    // DTOs
    public class UserProfile
    {
        public long id { get; set; }
        public string nick { get; set; } = string.Empty;
        public bool is_admin { get; set; }
        public DateTime created_at { get; set; }
        public string? avatar_path { get; set; }
        public int followers_count { get; set; }
        public int following_count { get; set; }
        public int posts_count { get; set; }
    }

    public class UserSearchResult
    {
        public long id { get; set; }
        public string nick { get; set; } = string.Empty;
        public string? avatar_path { get; set; }
    }

    public class UserSummary
    {
        public long id { get; set; }
        public string nick { get; set; } = string.Empty;
        public string? avatar_path { get; set; }
        public DateTime relation_date { get; set; }
    }

    public class UserAdminView
    {
        public long id { get; set; }
        public string nick { get; set; } = string.Empty;
        public bool is_admin { get; set; }
        public DateTime created_at { get; set; }
        public string? avatar_path { get; set; }
        public bool is_banned { get; set; }
        public string? ban_reason { get; set; }
        public DateTime? ban_end_date { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string? nick { get; set; }
        public long? avatar_id { get; set; }
    }

    public class UpdateRoleRequest
    {
        public bool is_admin { get; set; }
    }
}