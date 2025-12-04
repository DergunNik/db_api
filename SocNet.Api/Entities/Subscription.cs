namespace SocNet.Api.Entities;

public class Subscription
{
    public long UserFromId { get; set; }
    public long UserToId { get; set; }
    public DateTime CreatedAt { get; set; }
}
