namespace SocNet.Api.Entities;

public class Like
{
    public long PostId { get; set; }
    public long UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
