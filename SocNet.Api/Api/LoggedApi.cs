using System.Data;
using Dapper;
using Npgsql;

namespace SocNet.Api.Api;

public abstract class LoggedApi
{
    private readonly string _connectionString;

    protected LoggedApi(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
                            ?? throw new Exception("Connection string not found");
    }

    public async Task LogAction(long? userId, string action)
    {
        try
        {
            using IDbConnection db = new NpgsqlConnection(_connectionString);
            await db.ExecuteAsync(
                "INSERT INTO log (user_id, details) VALUES (@userId, @details)",
                new { userId, details = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}: {action}" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log action: {ex.Message}");
        }
    }
}
