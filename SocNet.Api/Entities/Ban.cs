namespace SocNet.Api.Entities;

public class Ban
{
    public long Id { get; set; }
    public long BannedUserId { get; set; }
    public long? AdminId { get; set; }
    public long? ReportId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Reason { get; set; }
}
