using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using SocNet.Api.Mongo;

namespace SocNet.Api.Api;

public abstract class LoggedApi
{
    private readonly string _connectionString;
    protected readonly IDistributedCache Cache;
    protected readonly MongoLogService LogService;

    protected LoggedApi(IConfiguration config, IDistributedCache cache, MongoLogService logService)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
                            ?? throw new Exception("Connection string not found");
        Cache = cache;
        LogService = logService;
    }

    public string ConnectionString => _connectionString;

    public async Task LogAction(long? userId, string action)
    {
        try
        {
            await LogService.LogAsync(new LogEvent 
            { 
                UserId = userId, 
                EventType = LogEventType.UserAction, 
                Details = action 
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log action to MongoDB: {ex.Message}");
        }
    }

    public async Task LogDbQuery(long? userId, string query)
    {
        try
        {
            await LogService.LogAsync(new LogEvent 
            { 
                UserId = userId, 
                EventType = LogEventType.DatabaseQuery, 
                Details = query 
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log query to MongoDB: {ex.Message}");
        }
    }

    public async Task LogException(long? userId, Exception exception)
    {
        try
        {
            await LogService.LogAsync(new LogEvent 
            { 
                UserId = userId, 
                EventType = LogEventType.Exception, 
                Details = exception.ToString() 
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log exception to MongoDB: {ex.Message}");
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

    public async Task InvalidateUserCache(long userId)
    {
        await Cache.RemoveAsync($"user:status:ban:{userId}");
    }
}