namespace SocNet.Api.Entities;

public class Favorite
{
    public long user_id { get; set; }
    public long post_id { get; set; }
    public DateTime created_at { get; set; }
}
