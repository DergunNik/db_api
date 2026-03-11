using MongoDB.Driver;
using MongoDB.Bson;

namespace SocNet.Api.Mongo;

public class MongoLogService
{
    private readonly IMongoCollection<LogEvent> _logs;
    private const int AnomalyCnt = 20;

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

    public async Task<List<BsonDocument>> GetTopActiveUsersAsync()
    {
        var pipeline = new BsonDocument[]
        {
            new BsonDocument("$match", new BsonDocument("userId", new BsonDocument("$ne", BsonNull.Value))),
            new BsonDocument("$group", new BsonDocument 
            { 
                { "_id", "$userId" }, 
                { "Count", new BsonDocument("$sum", 1) } 
            }),
            new BsonDocument("$project", new BsonDocument 
            { 
                { "UserId", new BsonDocument("$toString", "$_id") }, 
                { "Count", 1 }, 
                { "_id", 0 } 
            }),
            new BsonDocument("$sort", new BsonDocument("Count", -1)),
            new BsonDocument("$limit", 10)
        };

        return await _logs.Aggregate<BsonDocument>(pipeline).ToListAsync();
    }

    public async Task<List<BsonDocument>> GetActivityTimelineAsync()
    {
        var pipeline = new BsonDocument[]
        {
            new BsonDocument("$group", new BsonDocument 
            { 
                { "_id", new BsonDocument("$dateToString", new BsonDocument { { "format", "%Y-%m-%d" }, { "date", "$createdAt" } }) }, 
                { "Count", new BsonDocument("$sum", 1) } 
            }),
            new BsonDocument("$project", new BsonDocument 
            { 
                { "Date", "$_id" }, 
                { "Count", 1 }, 
                { "_id", 0 } 
            }),
            new BsonDocument("$sort", new BsonDocument("Date", 1))
        };

        return await _logs.Aggregate<BsonDocument>(pipeline).ToListAsync();
    }

    public async Task<List<BsonDocument>> GetHourlyTrendsAsync()
    {
        var pipeline = new BsonDocument[]
        {
            new BsonDocument("$group", new BsonDocument 
            { 
                { "_id", new BsonDocument("$hour", "$CreatedAt") }, 
                { "Count", new BsonDocument("$sum", 1) } 
            }),
            new BsonDocument("$project", new BsonDocument 
            { 
                { "Hour", "$_id" }, 
                { "Count", 1 }, 
                { "_id", 0 } 
            }),
            new BsonDocument("$sort", new BsonDocument("Hour", 1))
        };

        return await _logs.Aggregate<BsonDocument>(pipeline).ToListAsync();
    }
    
    public async Task<List<BsonDocument>> GetCrudDistributionAsync()
    {
        var pipeline = new BsonDocument[]
        {
            // Исправлено: eventType -> EventType
            new BsonDocument("$match", new BsonDocument("EventType", "DatabaseQuery")),
            new BsonDocument("$project", new BsonDocument("Op", 
                new BsonDocument("$toUpper", 
                    // Исправлено: details -> Details
                    new BsonDocument("$arrayElemAt", new BsonArray { new BsonDocument("$split", new BsonArray { "$Details", " " }), 0 })
                )
            )),
            new BsonDocument("$group", new BsonDocument 
            { 
                { "_id", "$Op" }, 
                { "Count", new BsonDocument("$sum", 1) } 
            }),
            new BsonDocument("$project", new BsonDocument 
            { 
                { "Operation", "$_id" }, 
                { "Count", 1 }, 
                { "_id", 0 } 
            }),
            new BsonDocument("$sort", new BsonDocument("Count", -1))
        };

        return await _logs.Aggregate<BsonDocument>(pipeline).ToListAsync();
    }

    public async Task<List<BsonDocument>> GetAnomaliesAsync()
    {
        var pipeline = new BsonDocument[]
        {
            // Исправлено: userId -> UserId
            new BsonDocument("$match", new BsonDocument("UserId", new BsonDocument("$ne", BsonNull.Value))),
            new BsonDocument("$group", new BsonDocument 
            { 
                { "_id", new BsonDocument 
                    { 
                        { "u", "$UserId" }, 
                        // Исправлено: createdAt -> CreatedAt
                        { "m", new BsonDocument("$dateToString", new BsonDocument { { "format", "%Y-%m-%dT%H:%M" }, { "date", "$CreatedAt" } }) } 
                    } 
                }, 
                { "Count", new BsonDocument("$sum", 1) } 
            }),
            new BsonDocument("$match", new BsonDocument("Count", new BsonDocument("$gt", AnomalyCnt))),
            new BsonDocument("$project", new BsonDocument 
            { 
                { "UserId", "$_id.u" }, 
                { "Minute", "$_id.m" }, 
                { "Count", 1 }, 
                { "_id", 0 } 
            }),
            new BsonDocument("$sort", new BsonDocument("Count", -1))
        };

        return await _logs.Aggregate<BsonDocument>(pipeline).ToListAsync();
    }
}