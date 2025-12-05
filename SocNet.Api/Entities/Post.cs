namespace SocNet.Api.Entities;

public class Post
{
    public long id { get; set; }
    public string? text { get; set; }
    public long author_id { get; set; }
    public long? answer_to_id { get; set; }
    public DateTime created_at { get; set; }
}
