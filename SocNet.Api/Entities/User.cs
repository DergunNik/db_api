namespace SocNet.Api.Entities;

public class User
{
    public long Id { get; set; }
    public string Nick { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public long? AvatarId { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}
