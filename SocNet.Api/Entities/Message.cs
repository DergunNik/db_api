namespace SocNet.Api.Entities;

public class Message
{
    public long id { get; set; }
    public long? author_id { get; set; }
    public long? media_id { get; set; }
    public long chat_id { get; set; }
    public DateTime created_at { get; set; }
    public string content { get; set; } = string.Empty;
}
