using System.Data;
using System.Security.Claims;
using Dapper;
using Npgsql;

namespace SocNet.Api.Api;

public static class ReportApi
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var loggedApi = new ReportApiLogged(config);

        var group = routes.MapGroup("/reports")
            .RequireAuthorization()
            .WithTags("Reports");

        group.MapPost("/user/{targetUserId:long}", async (long targetUserId, CreateReportRequest req, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (await loggedApi.IsUserBanned(userId))
                return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var targetExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM \"user\" WHERE id = @targetUserId)",
                new { targetUserId });

            if (!targetExists)
                return Results.NotFound("Target user not found");

            var reportId = await db.QueryFirstAsync<long>(
                @"INSERT INTO report (author_id, target_user_id, comment)
                  VALUES (@authorId, @targetUserId, @comment)
                  RETURNING id",
                new { authorId = userId, targetUserId, comment = req.comment });

            await loggedApi.LogAction(userId, $"Created report {reportId} on user {targetUserId}");

            return Results.Created($"/reports/{reportId}", new { reportId });
        });

        group.MapPost("/post/{postId:long}", async (long postId, CreateReportRequest req, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (await loggedApi.IsUserBanned(userId))
                return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var postExists = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM post WHERE id = @postId)",
                new { postId });

            if (!postExists)
                return Results.NotFound("Post not found");

            var reportId = await db.QueryFirstAsync<long>(
                @"INSERT INTO report (author_id, post_id, comment)
                  VALUES (@authorId, @postId, @comment)
                  RETURNING id",
                new { authorId = userId, postId, comment = req.comment });

            await loggedApi.LogAction(userId, $"Created report {reportId} on post {postId}");

            return Results.Created($"/reports/{reportId}", new { reportId });
        });

        group.MapGet("/{reportId:long}", async (long reportId, ClaimsPrincipal user) =>
        {
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = user.IsInRole("Admin");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var report = await db.QueryFirstOrDefaultAsync<ReportDetails>(
                @"SELECT r.id, r.comment, r.is_reviewed, r.created_at,
                         ua.nick as author_nick,
                         ut.nick as target_user_nick,
                         p.text as post_text
                  FROM report r
                  LEFT JOIN ""user"" ua ON r.author_id = ua.id
                  LEFT JOIN ""user"" ut ON r.target_user_id = ut.id
                  LEFT JOIN post p ON r.post_id = p.id
                  WHERE r.id = @reportId AND (r.author_id = @userId OR @isAdmin)",
                new { reportId, userId, isAdmin });

            return report is not null ? Results.Ok(report) : Results.NotFound();
        });

        var adminGroup = routes.MapGroup("/admin/reports")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithTags("Admin");

        adminGroup.MapGet("/", async (bool? reviewed, int page = 1, int pageSize = 50) =>
        {
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var reports = await db.QueryAsync<ReportDetails>(
                @"SELECT r.id, r.comment, r.is_reviewed, r.created_at,
                         ua.nick as author_nick,
                         ut.nick as target_user_nick,
                         p.text as post_text
                  FROM report r
                  LEFT JOIN ""user"" ua ON r.author_id = ua.id
                  LEFT JOIN ""user"" ut ON r.target_user_id = ut.id
                  LEFT JOIN post p ON r.post_id = p.id
                  WHERE (@reviewed IS NULL OR r.is_reviewed = @reviewed)
                  ORDER BY r.created_at DESC
                  LIMIT @pageSize OFFSET @offset",
                new { reviewed, pageSize, offset = (page - 1) * pageSize });

            return Results.Ok(reports);
        });

        adminGroup.MapPut("/{reportId:long}/reviewed", async (long reportId, ClaimsPrincipal user) =>
        {
            var adminId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await db.ExecuteAsync(
                "UPDATE report SET is_reviewed = true WHERE id = @reportId",
                new { reportId });

            await loggedApi.LogAction(adminId, $"Marked report {reportId} as reviewed");

            return Results.Ok();
        });

        adminGroup.MapPost("/{reportId:long}/ban", async (long reportId, CreateBanRequest req, ClaimsPrincipal user) =>
        {
            var adminId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var report = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT target_user_id FROM report WHERE id = @reportId",
                new { reportId });

            if (report == null)
                return Results.NotFound("Report not found");

            var bannedUserId = (long)report.target_user_id;

            var banId = await db.QueryFirstAsync<long>(
                @"INSERT INTO ban (banned_user_id, admin_id, report_id, end_date, reason)
                  VALUES (@bannedUserId, @adminId, @reportId, @endDate, @reason)
                  RETURNING id",
                new
                {
                    bannedUserId,
                    adminId,
                    reportId,
                    endDate = req.end_date,
                    reason = req.reason
                });

            await db.ExecuteAsync(
                "UPDATE report SET is_reviewed = true WHERE id = @reportId",
                new { reportId });

            await loggedApi.LogAction(adminId, $"Banned user {bannedUserId} for report {reportId}");

            return Results.Created($"/admin/bans/{banId}", new { banId });
        });

        adminGroup.MapDelete("/bans/{banId:long}", async (long banId, ClaimsPrincipal user) =>
        {
            var adminId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var ban = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT banned_user_id FROM ban WHERE id = @banId",
                new { banId });

            if (ban == null)
                return Results.NotFound("Ban not found");

            var bannedUserId = (long)ban.banned_user_id;

            await db.ExecuteAsync(
                "UPDATE ban SET end_date = now() - interval '1 minute' WHERE id = @banId",
                new { banId });

            await loggedApi.LogAction(adminId, $"Unbanned user {bannedUserId}");

            return Results.Ok();
        });

        adminGroup.MapGet("/bans", async (int page = 1, int pageSize = 50) =>
        {
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            var bans = await db.QueryAsync<BanDetails>(
                @"SELECT b.id, b.start_date, b.end_date, b.reason,
                         bu.nick as banned_user_nick,
                         au.nick as admin_nick,
                         r.comment as report_comment
                  FROM ban b
                  JOIN ""user"" bu ON b.banned_user_id = bu.id
                  LEFT JOIN ""user"" au ON b.admin_id = au.id
                  LEFT JOIN report r ON b.report_id = r.id
                  WHERE b.end_date IS NULL OR b.end_date > now()
                  ORDER BY b.start_date DESC
                  LIMIT @pageSize OFFSET @offset",
                new { pageSize, offset = (page - 1) * pageSize });

            return Results.Ok(bans);
        });

        return routes;
    }

    private class ReportApiLogged : LoggedApi
    {
        public string ConnectionString { get; }

        public ReportApiLogged(IConfiguration config) : base(config)
        {
            ConnectionString = config.GetConnectionString("DefaultConnection")
                              ?? throw new Exception("Connection string not found");
        }

        public async Task<bool> IsUserBanned(long userId)
        {
            using IDbConnection db = new NpgsqlConnection(ConnectionString);
            var isBanned = await db.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS(
                    SELECT 1 FROM ban
                    WHERE banned_user_id = @userId
                      AND (end_date IS NULL OR end_date > now())
                  )",
                new { userId });
            return isBanned;
        }
    }

    public class CreateReportRequest
    {
        public string comment { get; set; } = string.Empty;
    }

    public class CreateBanRequest
    {
        public DateTime? end_date { get; set; }
        public string reason { get; set; } = string.Empty;
    }

    public class ReportDetails
    {
        public long id { get; set; }
        public string? comment { get; set; }
        public bool is_reviewed { get; set; }
        public DateTime created_at { get; set; }
        public string? author_nick { get; set; }
        public string? target_user_nick { get; set; }
        public string? post_text { get; set; }
    }

    public class BanDetails
    {
        public long id { get; set; }
        public DateTime start_date { get; set; }
        public DateTime? end_date { get; set; }
        public string? reason { get; set; }
        public string banned_user_nick { get; set; } = string.Empty;
        public string? admin_nick { get; set; }
        public string? report_comment { get; set; }
    }
}
