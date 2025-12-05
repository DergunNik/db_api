namespace SocNet.Api.Entities;

public class Report
{
    public long id { get; set; }
    public long? author_id { get; set; }
    public long? target_user_id { get; set; }
    public long? post_id { get; set; }
    public string? comment { get; set; }
    public bool is_reviewed { get; set; }
    public DateTime created_at { get; set; }
}
