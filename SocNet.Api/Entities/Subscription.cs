namespace SocNet.Api.Entities;

public class Subscription
{
    public long user_from_id { get; set; }
    public long user_to_id { get; set; }
    public DateTime created_at { get; set; }
}
