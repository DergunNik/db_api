using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SocNet.Api.Mongo;

public enum LogEventType
{
    UserAction,
    DatabaseQuery,
    Exception
}

public class LogEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public long? UserId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public LogEventType EventType { get; set; }

    public string Details { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}