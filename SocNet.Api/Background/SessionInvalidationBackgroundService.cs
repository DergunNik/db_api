using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace SocNet.Api.Background;

public class SessionInvalidationBackgroundService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _memoryCache;

    public SessionInvalidationBackgroundService(
        IConnectionMultiplexer redis, 
        IMemoryCache memoryCache)
    {
        _redis = redis;
        _memoryCache = memoryCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();
        
        await subscriber.SubscribeAsync("session-revoked", (channel, message) =>
        {
            var sessionKey = message.ToString();
            if (!string.IsNullOrEmpty(sessionKey))
            {
                _memoryCache.Remove(sessionKey);
            }
        });
    }
}