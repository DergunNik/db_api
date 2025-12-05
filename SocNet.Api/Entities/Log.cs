namespace SocNet.Api.Entities;

public class Log
{
    public long id { get; set; }
    public DateTime time { get; set; }
    public long? user_id { get; set; }
    public string? details { get; set; }
}
