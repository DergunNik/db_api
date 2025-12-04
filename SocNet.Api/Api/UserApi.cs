using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using SocNet.Api.Entities;

namespace SocNet.Api.Api;

public static class UserApi
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var loggedApi = new UserApiLogged(config);

        var group = routes.MapGroup("/users")
            .RequireAuthorization();

        group.MapGet("/profile", async (ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
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

            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        });

        group.MapGet("/search", async (string query, int limit = 20) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Results.BadRequest("Search query must be at least 2 characters");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            var users = await db.QueryAsync<UserSearchResult>(
                @"SELECT u.id, u.nick, m.file_path as avatar_path
                  FROM ""user"" u
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE u.nick ILIKE @query
                  ORDER BY u.nick
                  LIMIT @limit",
                new { query = $"%{query}%", limit });

            return Results.Ok(users);
        });

        group.MapPut("/profile", async (UpdateProfileRequest req, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            if (!string.IsNullOrWhiteSpace(req.Nick))
            {
                var existing = await db.QueryFirstOrDefaultAsync<long?>(
                    @"SELECT id FROM ""user"" WHERE nick = @nick AND id != @userId",
                    new { nick = req.Nick, userId });

                if (existing.HasValue)
                    return Results.Conflict("Nick already taken");
            }

            var updateFields = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("userId", userId);

            if (!string.IsNullOrWhiteSpace(req.Nick))
            {
                updateFields.Add("nick = @nick");
                parameters.Add("nick", req.Nick);
            }

            if (req.AvatarId.HasValue)
            {
                updateFields.Add("avatar_id = @avatarId");
                parameters.Add("avatarId", req.AvatarId.Value);
            }

            if (updateFields.Any())
            {
                var sql = $"UPDATE \"user\" SET {string.Join(", ", updateFields)} WHERE id = @userId";
                await db.ExecuteAsync(sql, parameters);

                await loggedApi.LogAction(userId, "Profile updated");
            }

            return Results.Ok();
        });

        group.MapGet("/{userId:long}/followers", async (long userId, int page = 1, int pageSize = 20) =>
        {
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            var followers = await db.QueryAsync<UserSummary>(
                @"SELECT u.id, u.nick, m.file_path as avatar_path, s.created_at as relation_date
                  FROM subscription s
                  JOIN ""user"" u ON s.user_from_id = u.id
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE s.user_to_id = @userId
                  ORDER BY s.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            return Results.Ok(followers);
        });

        group.MapGet("/{userId:long}/following", async (long userId, int page = 1, int pageSize = 20) =>
        {
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            var following = await db.QueryAsync<UserSummary>(
                @"SELECT u.id, u.nick, m.file_path as avatar_path, s.created_at as relation_date
                  FROM subscription s
                  JOIN ""user"" u ON s.user_to_id = u.id
                  LEFT JOIN media m ON u.avatar_id = m.id
                  WHERE s.user_from_id = @userId
                  ORDER BY s.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { userId, pageSize, offset = (page - 1) * pageSize });

            return Results.Ok(following);
        });

        var adminGroup = routes.MapGroup("/admin/users")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminGroup.MapGet("/", async (int page = 1, int pageSize = 50) =>
        {
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
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

            return Results.Ok(users);
        });

        adminGroup.MapPut("/{userId:long}/role", async (long userId, UpdateRoleRequest req, ClaimsPrincipal user) =>
        {
            var adminId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync(
                "UPDATE \"user\" SET is_admin = @isAdmin WHERE id = @userId",
                new { userId, isAdmin = req.IsAdmin });

            await loggedApi.LogAction(adminId, $"Changed user {userId} role to {(req.IsAdmin ? "admin" : "user")}");

            return Results.Ok();
        });

        adminGroup.MapDelete("/{userId:long}", async (long userId, ClaimsPrincipal user) =>
        {
            var adminId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync("DELETE FROM \"user\" WHERE id = @userId", new { userId });

            await loggedApi.LogAction(adminId, $"Deleted user {userId}");

            return Results.Ok();
        });

        return routes;
    }

    private class UserApiLogged : LoggedApi
    {
        public string ConnectionString { get; }

        public UserApiLogged(IConfiguration config) : base(config)
        {
            ConnectionString = config.GetConnectionString("DefaultConnection")
                              ?? throw new Exception("Connection string not found");
        }
    }

    // DTOs
    public record UserProfile(
        long Id,
        string Nick,
        bool IsAdmin,
        DateTime CreatedAt,
        string? AvatarPath,
        int FollowersCount,
        int FollowingCount,
        int PostsCount
    );

    public record UserSearchResult(long Id, string Nick, string? AvatarPath);

    public record UserSummary(long Id, string Nick, string? AvatarPath, DateTime RelationDate);

    public record UserAdminView(
        long Id,
        string Nick,
        bool IsAdmin,
        DateTime CreatedAt,
        string? AvatarPath,
        bool IsBanned,
        string? BanReason,
        DateTime? BanEndDate
    );

    public record UpdateProfileRequest(string? Nick, long? AvatarId);

    public record UpdateRoleRequest(bool IsAdmin);
}
