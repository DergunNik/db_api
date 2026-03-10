using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;

namespace SocNet.Api.Api;

public abstract class LoggedApi
{
    private readonly string _connectionString;
    protected readonly IDistributedCache Cache;

    protected LoggedApi(IConfiguration config, IDistributedCache cache)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
                            ?? throw new Exception("Connection string not found");
        Cache = cache;
    }

    public string ConnectionString => _connectionString;

    public async Task LogAction(long? userId, string action)
    {
        try
        {
            using IDbConnection db = new NpgsqlConnection(_connectionString);
            await db.ExecuteAsync(
                "INSERT INTO log (user_id, details) VALUES (@userId, @details)",
                new { userId, details = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}: {action}" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log action: {ex.Message}");
        }
    }

    public async Task<bool> IsUserBanned(long userId)
    {
        string cacheKey = $"user:status:ban:{userId}";
        var cachedValue = await Cache.GetStringAsync(cacheKey);

        if (cachedValue != null)
        {
            return bool.Parse(cachedValue);
        }

        using IDbConnection db = new NpgsqlConnection(_connectionString);
        var isBanned = await db.QueryFirstOrDefaultAsync<bool>(
            @"SELECT EXISTS(
                SELECT 1 FROM ban 
                WHERE banned_user_id = @userId 
                AND (end_date IS NULL OR end_date > now())
            )",
            new { userId });

        await Cache.SetStringAsync(cacheKey, isBanned.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        return isBanned;
    }

    public async Task<string[]> GetUserRoles(long userId)
    {
        string cacheKey = $"user:roles:{userId}";
        var cached = await Cache.GetStringAsync(cacheKey);

        if (cached != null)
        {
            return JsonSerializer.Deserialize<string[]>(cached) ?? Array.Empty<string>();
        }

        using IDbConnection db = new NpgsqlConnection(_connectionString);
        var roles = (await db.QueryAsync<string>(
            @"SELECT r.name FROM role r 
              JOIN user_role ur ON r.id = ur.role_id 
              WHERE ur.user_id = @userId",
            new { userId })).ToArray();

        await Cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(roles), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        });

        return roles;
    }

    public async Task InvalidateUserCache(long userId)
    {
        await Cache.RemoveAsync($"user:status:ban:{userId}");
        await Cache.RemoveAsync($"user:roles:{userId}");
    }
}