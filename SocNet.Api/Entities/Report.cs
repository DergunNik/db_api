namespace SocNet.Api.Entities;

public class Report
{
    public long Id { get; set; }
    public long? AuthorId { get; set; }
    public long? TargetUserId { get; set; }
    public long? PostId { get; set; }
    public string? Comment { get; set; }
    public bool IsReviewed { get; set; }
    public DateTime CreatedAt { get; set; }
}
