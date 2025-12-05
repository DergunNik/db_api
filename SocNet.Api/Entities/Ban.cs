namespace SocNet.Api.Entities;

public class Ban
{
    public long id { get; set; }
    public long banned_user_id { get; set; }
    public long? admin_id { get; set; }
    public long? report_id { get; set; }
    public DateTime start_date { get; set; }
    public DateTime? end_date { get; set; }
    public string? reason { get; set; }
}
