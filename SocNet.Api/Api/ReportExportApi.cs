using Microsoft.AspNetCore.Mvc;
using System.Text;
using MongoDB.Bson;
using SocNet.Api.Mongo;

namespace SocNet.Api.Api;

public static class ReportExportApi
{
    public static IEndpointRouteBuilder MapReportExportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin/analytics")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithTags("Analytics Reports");

        group.MapGet("/top-users", async (MongoLogService logService, [FromQuery] string format = "json") =>
        {
            var data = await logService.GetTopActiveUsersAsync();
            return ExportData(data, format, "UserId", "Count");
        });

        group.MapGet("/timeline", async (MongoLogService logService, [FromQuery] string format = "json") =>
        {
            var data = await logService.GetActivityTimelineAsync();
            return ExportData(data, format, "Date", "Count");
        });

        group.MapGet("/crud-stats", async (MongoLogService logService, [FromQuery] string format = "json") =>
        {
            var data = await logService.GetCrudDistributionAsync();
            return ExportData(data, format, "Operation", "Count");
        });
        
        group.MapGet("/anomalies", async (MongoLogService logService, [FromQuery] string format = "json") =>
        {
            var data = await logService.GetAnomaliesAsync();
            
            var jsonReadyData = data.Select(d => new Dictionary<string, object>
            {
                { "AnomalyTarget", $"UserId: {d["UserId"]}, Minute: {d["Minute"]}" },
                { "Count", d["Count"].ToInt32() }
            }).ToList();

            return ProcessExport(jsonReadyData, format, "AnomalyTarget", "Count");
        });
        
        group.MapGet("/hourly-trends", async (MongoLogService logService, [FromQuery] string format = "json") =>
        {
            var data = await logService.GetHourlyTrendsAsync();
    
            var jsonReadyData = data.Select(d => new Dictionary<string, object>
            {
                { "Hour", d["Hour"].ToInt32() },
                { "Count", d["Count"].ToInt32() }
            }).ToList();

            return ProcessExport(jsonReadyData, format, "Hour", "Count");
        });
        
        return routes;
    }
    
    

    private static IResult ExportData(List<BsonDocument> data, string format, string keyName, string valueName)
    {
        var jsonReadyData = data.Select(d => new Dictionary<string, object>
        {
            { keyName, d[keyName].ToString()! },
            { valueName, d[valueName].ToInt32() }
        }).ToList();

        return ProcessExport(jsonReadyData, format, keyName, valueName);
    }

    private static IResult ProcessExport(List<Dictionary<string, object>> data, string format, string keyName, string valueName)
    {
        if (format.ToLower() == "csv")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{keyName},{valueName}"); 
            
            foreach (var item in data)
            {
                sb.AppendLine($"{item[keyName]},{item[valueName]}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return Results.File(bytes, "text/csv", $"report_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        return Results.Ok(data);
    }
}