using System.Data;
using System.Security.Claims;
using Dapper;
using Npgsql;

namespace SocNet.Api.Api;

public static class SubscriptionApi
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var loggedApi = new SubscriptionApiLogged(config);

        var group = routes.MapGroup("/subscriptions")
            .RequireAuthorization()
            .WithTags("Subs");

        group.MapPost("/{targetUserId:long}", async (long targetUserId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (userId == targetUserId)
                return Results.BadRequest("Cannot subscribe to yourself");

            if (await loggedApi.IsUserBanned(userId))
                return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var targetExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM \"user\" WHERE id = @targetUserId)",
                new { targetUserId });

            if (!targetExists)
                return Results.NotFound("Target user not found");

            await db.ExecuteAsync(
                @"INSERT INTO subscription (user_from_id, user_to_id)
                  VALUES (@userId, @targetUserId)
                  ON CONFLICT (user_from_id, user_to_id) DO NOTHING",
                new { userId, targetUserId });

            await loggedApi.LogAction(userId, $"Subscribed to user {targetUserId}");

            return Results.Ok();
        });

        group.MapDelete("/{targetUserId:long}", async (long targetUserId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync(
                @"DELETE FROM subscription
                  WHERE user_from_id = @userId AND user_to_id = @targetUserId",
                new { userId, targetUserId });

            await loggedApi.LogAction(userId, $"Unsubscribed from user {targetUserId}");

            return Results.Ok();
        });

        group.MapGet("/{targetUserId:long}/status", async (long targetUserId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var isSubscribed = await db.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS(
                    SELECT 1 FROM subscription
                    WHERE user_from_id = @userId AND user_to_id = @targetUserId
                )",
                new { userId, targetUserId });

            return Results.Ok(new { isSubscribed });
        });

        group.MapGet("/counts", async (ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var counts = await db.QueryFirstAsync<SubscriptionCounts>(
                @"SELECT
                    (SELECT COUNT(*) FROM subscription WHERE user_to_id = @userId) as followers_count,
                    (SELECT COUNT(*) FROM subscription WHERE user_from_id = @userId) as following_count",
                new { userId });

            return Results.Ok(counts);
        });

        return routes;
    }

    private class SubscriptionApiLogged : LoggedApi
    {
        public string ConnectionString { get; }

        public SubscriptionApiLogged(IConfiguration config) : base(config)
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

    public class SubscriptionCounts
    {
        public int followers_count { get; set; }
        public int following_count { get; set; }
    }
}
