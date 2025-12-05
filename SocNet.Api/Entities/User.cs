namespace SocNet.Api.Entities;

public class User
{
    public long id { get; set; }
    public string nick { get; set; } = string.Empty;
    public string password_hash { get; set; } = string.Empty;
    public long? avatar_id { get; set; }
    public bool is_admin { get; set; }
    public DateTime created_at { get; set; }
}
