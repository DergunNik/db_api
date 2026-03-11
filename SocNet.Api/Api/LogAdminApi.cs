using Microsoft.AspNetCore.Mvc;
using SocNet.Api.Mongo;

namespace SocNet.Api.Api;

public static class LogAdminApi
{
    public static IEndpointRouteBuilder MapLogAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin/logs")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithTags("Admin Logs");

        group.MapGet("/", async (
            [FromQuery] long? userId,
            [FromQuery] LogEventType? type,
            [FromQuery] int? windowMinutes,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            MongoLogService logService = null!) =>
        {
            if (windowMinutes.HasValue)
            {
                from = DateTime.UtcNow.AddMinutes(-windowMinutes.Value);
            }

            var (items, total) = await logService.GetLogsAsync(userId, type, from, to, page, pageSize);
            
            return Results.Ok(new
            {
                filters = new { userId, type, windowMinutes, usedFromDate = from, usedToDate = to },
                pagination = new { 
                    total, 
                    page, 
                    pageSize, 
                    totalPages = (int)Math.Ceiling((double)total / pageSize) 
                },
                logs = items
            });
        });

        group.MapGet("/user/{userId:long}", async (long userId, MongoLogService logService, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) => 
        {
            var (items, total) = await logService.GetLogsAsync(userId, null, null, null, page, pageSize);
            return Results.Ok(new { userId, total, logs = items });
        });

        group.MapGet("/errors", async (MongoLogService logService, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) => 
        {
            var (items, total) = await logService.GetLogsAsync(null, LogEventType.Exception, null, null, page, pageSize);
            return Results.Ok(new { total, logs = items });
        });

        return routes;
    }
}