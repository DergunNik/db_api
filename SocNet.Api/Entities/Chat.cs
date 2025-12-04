namespace SocNet.Api.Entities;

public class Chat
{
    public long Id { get; set; }
    public long FirstUserId { get; set; }
    public long SecondUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
