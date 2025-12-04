namespace SocNet.Api.Entities;

public class Post
{
    public long Id { get; set; }
    public string? Text { get; set; }
    public long AuthorId { get; set; }
    public long? AnswerToId { get; set; }
    public DateTime CreatedAt { get; set; }
}
