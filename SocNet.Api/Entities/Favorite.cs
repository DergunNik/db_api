namespace SocNet.Api.Entities;

public class Favorite
{
    public long UserId { get; set; }
    public long PostId { get; set; }
    public DateTime CreatedAt { get; set; }
}
