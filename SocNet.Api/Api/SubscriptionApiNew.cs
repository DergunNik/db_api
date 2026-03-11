using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using SocNet.Api.Mongo;

namespace SocNet.Api.Api;

public static class SubscriptionApi
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var group = routes.MapGroup("/subscriptions")
            .RequireAuthorization()
            .WithTags("Subs");

        group.MapPost("/{targetUserId:long}", async (long targetUserId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new SubscriptionApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (userId == targetUserId)
                return Results.BadRequest("Cannot subscribe to yourself");

            if (await loggedApi.IsUserBanned(userId))
                return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var targetExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM \"user\" WHERE id = @targetUserId)",
                new { targetUserId }
            );

            if (!targetExists)
                return Results.NotFound("Target user not found");

            await loggedApi.LogDbQuery(userId, $"INSERT subscription attempt to user {targetUserId}");
            await db.ExecuteAsync(
                @"INSERT INTO subscription (user_from_id, user_to_id)
          VALUES (@userId, @targetUserId)
          ON CONFLICT (user_from_id, user_to_id) DO NOTHING",
                new { userId, targetUserId });

            await InvalidateSubscriptionCache(cache, userId, targetUserId);

            await loggedApi.LogAction(userId, $"Subscribed to user {targetUserId}");
            return Results.Ok();
        });

        group.MapDelete("/{targetUserId:long}", async (long targetUserId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new SubscriptionApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await loggedApi.LogDbQuery(userId, $"DELETE subscription from user {targetUserId}");
            await db.ExecuteAsync(
                @"DELETE FROM subscription
                  WHERE user_from_id = @userId AND user_to_id = @targetUserId",
                new { userId, targetUserId });

            await InvalidateSubscriptionCache(cache, userId, targetUserId);

            await loggedApi.LogAction(userId, $"Unsubscribed from user {targetUserId}");
            return Results.Ok();
        });

        group.MapGet("/{targetUserId:long}/status", async (long targetUserId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new SubscriptionApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"sub:status:{userId}:{targetUserId}";

            var cachedStatus = await cache.GetStringAsync(cacheKey);
            if (cachedStatus != null) return Results.Ok(new { isSubscribed = bool.Parse(cachedStatus) });

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(userId, $"SELECT subscription status with {targetUserId}");
            var isSubscribed = await db.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS(SELECT 1 FROM subscription WHERE user_from_id = @userId AND user_to_id = @targetUserId)",
                new { userId, targetUserId });

            await cache.SetStringAsync(cacheKey, isSubscribed.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            return Results.Ok(new { isSubscribed });
        });

        group.MapGet("/counts", async (ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new SubscriptionApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string cacheKey = $"sub:counts:{userId}";

            var cachedCounts = await cache.GetStringAsync(cacheKey);
            if (cachedCounts != null) return Results.Ok(JsonSerializer.Deserialize<SubscriptionCounts>(cachedCounts));

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(userId, "SELECT subscription counts");
            var counts = await db.QueryFirstAsync<SubscriptionCounts>(
                @"SELECT
                    (SELECT COUNT(*) FROM subscription WHERE user_to_id = @userId) as followers_count,
                    (SELECT COUNT(*) FROM subscription WHERE user_from_id = @userId) as following_count",
                new { userId });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(counts), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Results.Ok(counts);
        });

        return routes;
    }

    private static async Task InvalidateSubscriptionCache(IDistributedCache cache, long userId, long targetUserId)
    {
        await cache.RemoveAsync($"sub:counts:{userId}");
        await cache.RemoveAsync($"sub:counts:{targetUserId}");
        await cache.RemoveAsync($"sub:status:{userId}:{targetUserId}");
        await cache.RemoveAsync($"feed:{userId}:p:1");
    }

    private class SubscriptionApiLogged : LoggedApi
    {
        public SubscriptionApiLogged(IConfiguration config, IDistributedCache cache, MongoLogService logService) 
            : base(config, cache, logService) { }
    }

    public class SubscriptionCounts
    {
        public int followers_count { get; set; }
        public int following_count { get; set; }
    }
}