namespace SocNet.Api.Entities;

public class Message
{
    public long Id { get; set; }
    public long? AuthorId { get; set; }
    public long ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Content { get; set; } = string.Empty;
}
