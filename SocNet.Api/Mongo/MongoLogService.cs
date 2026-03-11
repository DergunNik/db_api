using MongoDB.Driver;

namespace SocNet.Api.Mongo;

public class MongoLogService
{
    private readonly IMongoCollection<LogEvent> _logs;

    public MongoLogService(IConfiguration config)
    {
        var connectionString = config.GetConnectionString("MongoDb") 
                               ?? throw new Exception("MongoDB connection string not found");
        
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase("SocNetLogs");
        _logs = database.GetCollection<LogEvent>("Logs");

        var indexKeysDefinition = Builders<LogEvent>.IndexKeys.Ascending(x => x.CreatedAt);
        var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30) };
        var indexModel = new CreateIndexModel<LogEvent>(indexKeysDefinition, indexOptions);
        
        _logs.Indexes.CreateOne(indexModel);
    }

    public async Task LogAsync(LogEvent logEvent)
    {
        await _logs.InsertOneAsync(logEvent);
    }

    public async Task<List<LogEvent>> GetLogsAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        long? userId = null, 
        LogEventType? eventType = null)
    {
        var builder = Builders<LogEvent>.Filter;
        var filter = builder.Empty;

        if (startDate.HasValue)
            filter &= builder.Gte(x => x.CreatedAt, startDate.Value);
        
        if (endDate.HasValue)
            filter &= builder.Lte(x => x.CreatedAt, endDate.Value);
        
        if (userId.HasValue)
            filter &= builder.Eq(x => x.UserId, userId.Value);
        
        if (eventType.HasValue)
            filter &= builder.Eq(x => x.EventType, eventType.Value);

        return await _logs.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<(IEnumerable<LogEvent> Items, long Total)> GetLogsAsync(
        long? userId, 
        LogEventType? type, 
        DateTime? from, 
        DateTime? to, 
        int page, 
        int pageSize)
    {
        var builder = Builders<LogEvent>.Filter;
        var filter = builder.Empty;

        if (userId.HasValue)
            filter &= builder.Eq(x => x.UserId, userId);

        if (type.HasValue)
            filter &= builder.Eq(x => x.EventType, type);

        if (from.HasValue)
            filter &= builder.Gte(x => x.CreatedAt, from.Value);

        if (to.HasValue)
            filter &= builder.Lte(x => x.CreatedAt, to.Value);

        var total = await _logs.CountDocumentsAsync(filter);
        var items = await _logs.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, total);
    }
}