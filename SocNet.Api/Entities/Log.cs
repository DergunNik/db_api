namespace SocNet.Api.Entities;

public class Log
{
    public long Id { get; set; }
    public DateTime Time { get; set; }
    public long? UserId { get; set; }
    public string? Details { get; set; }
}
