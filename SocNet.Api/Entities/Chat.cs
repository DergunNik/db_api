namespace SocNet.Api.Entities;

public class Chat
{
    public long id { get; set; }
    public long first_user_id { get; set; }
    public long second_user_id { get; set; }
    public DateTime created_at { get; set; }
}
