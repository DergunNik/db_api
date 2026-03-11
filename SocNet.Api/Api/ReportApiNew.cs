using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using SocNet.Api.Mongo;

namespace SocNet.Api.Api;

public static class ReportApi
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var group = routes.MapGroup("/reports")
            .RequireAuthorization()
            .WithTags("Reports");

        group.MapPost("/user/{targetUserId:long}", async (long targetUserId, CreateReportRequest req, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ReportApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (await loggedApi.IsUserBanned(userId))
                return Results.BadRequest("User is banned");

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);

            await loggedApi.LogDbQuery(userId, $"Creating report on user {targetUserId}");
            var reportId = await db.QueryFirstAsync<long>(
                @"INSERT INTO report (author_id, target_user_id, comment)
                  VALUES (@authorId, @targetUserId, @comment)
                  RETURNING id",
                new { authorId = userId, targetUserId, comment = req.comment });

            await cache.RemoveAsync("admin:reports:p:1");

            await loggedApi.LogAction(userId, $"Created report {reportId} on user {targetUserId}");
            return Results.Created($"/reports/{reportId}", new { reportId });
        });

        group.MapGet("/{reportId:long}", async (long reportId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ReportApiLogged(cfg, cache, logService);
            var userId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = user.IsInRole("Admin");

            string cacheKey = $"report:{reportId}";
            var cached = await cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                var r = JsonSerializer.Deserialize<ReportDetails>(cached);
                if (r != null && (r.author_id == userId || isAdmin)) return Results.Ok(r);
            }

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(userId, $"Fetching details for report {reportId}");
            var report = await db.QueryFirstOrDefaultAsync<ReportDetails>(
                @"SELECT r.*, ua.nick as author_nick, ut.nick as target_user_nick, p.text as post_text
                  FROM report r
                  LEFT JOIN ""user"" ua ON r.author_id = ua.id
                  LEFT JOIN ""user"" ut ON r.target_user_id = ut.id
                  LEFT JOIN post p ON r.post_id = p.id
                  WHERE r.id = @reportId", new { reportId });

            if (report == null) return Results.NotFound();
            
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(report), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            return (report.author_id == userId || isAdmin) ? Results.Ok(report) : Results.Forbid();
        });

        var adminGroup = routes.MapGroup("/admin/reports")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithTags("Admin");

        adminGroup.MapPost("/{reportId:long}/ban", async (long reportId, CreateBanRequest req, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ReportApiLogged(cfg, cache, logService);
            var adminId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(adminId, $"Admin fetching target for ban from report {reportId}");
            var report = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT target_user_id FROM report WHERE id = @reportId", new { reportId });

            if (report == null) return Results.NotFound("Report not found");

            long bannedUserId = (long)report.target_user_id;

            await loggedApi.LogDbQuery(adminId, $"Admin inserting ban for user {bannedUserId}");
            var banId = await db.QueryFirstAsync<long>(
                @"INSERT INTO ban (banned_user_id, admin_id, report_id, end_date, reason)
                  VALUES (@bannedUserId, @adminId, @reportId, @endDate, @reason)
                  RETURNING id",
                new { bannedUserId, adminId, reportId, endDate = req.end_date, reason = req.reason });

            await db.ExecuteAsync("UPDATE report SET is_reviewed = true WHERE id = @reportId", new { reportId });

            await loggedApi.InvalidateUserCache(bannedUserId);
            
            await cache.RemoveAsync("admin:bans:p:1");
            await cache.RemoveAsync($"report:{reportId}");

            await loggedApi.LogAction(adminId, $"Banned user {bannedUserId} for report {reportId}");
            return Results.Created($"/admin/bans/{banId}", new { banId });
        });

        adminGroup.MapDelete("/bans/{banId:long}", async (long banId, ClaimsPrincipal user, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new ReportApiLogged(cfg, cache, logService);
            var adminId = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(adminId, $"Admin fetching ban info for id {banId}");
            var ban = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT banned_user_id FROM ban WHERE id = @banId", new { banId });

            if (ban == null) return Results.NotFound("Ban not found");

            await loggedApi.LogDbQuery(adminId, $"Admin terminating ban {banId}");
            await db.ExecuteAsync(
                "UPDATE ban SET end_date = now() - interval '1 minute' WHERE id = @banId", new { banId });

            await loggedApi.InvalidateUserCache((long)ban.banned_user_id);
            await cache.RemoveAsync("admin:bans:p:1");

            await loggedApi.LogAction(adminId, $"Unbanned user {ban.banned_user_id}");
            return Results.Ok();
        });

        adminGroup.MapGet("/bans", async (IDistributedCache cache, IConfiguration cfg, MongoLogService logService, int page = 1) =>
        {
            var loggedApi = new ReportApiLogged(cfg, cache, logService);
            string cacheKey = $"admin:bans:p:{page}";
            
            var cached = await cache.GetStringAsync(cacheKey);
            if (cached != null) return Results.Ok(JsonSerializer.Deserialize<IEnumerable<BanDetails>>(cached));

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(null, "Admin fetching active bans list");
            var bans = await db.QueryAsync<BanDetails>(
                @"SELECT b.*, bu.nick as banned_user_nick, au.nick as admin_nick, r.comment as report_comment
                  FROM ban b
                  JOIN ""user"" bu ON b.banned_user_id = bu.id
                  LEFT JOIN ""user"" au ON b.admin_id = au.id
                  LEFT JOIN report r ON b.report_id = r.id
                  WHERE b.end_date IS NULL OR b.end_date > now()
                  ORDER BY b.start_date DESC LIMIT 50 OFFSET @offset", new { offset = (page - 1) * 50 });

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(bans), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
            return Results.Ok(bans);
        });

        return routes;
    }

    private class ReportApiLogged : LoggedApi
    {
        public ReportApiLogged(IConfiguration config, IDistributedCache cache, MongoLogService logService) 
            : base(config, cache, logService) { }
    }

    public class CreateReportRequest { public string comment { get; set; } = string.Empty; }
    public class CreateBanRequest { public DateTime? end_date { get; set; } public string reason { get; set; } = string.Empty; }
    
    public class ReportDetails
    {
        public long id { get; set; }
        public long author_id { get; set; }
        public string? comment { get; set; }
        public bool is_reviewed { get; set; }
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
    }
}