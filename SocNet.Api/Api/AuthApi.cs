using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SocNet.Api.Entities;

namespace SocNet.Api.Api;

public static class AuthApi
{
    private static string? _connectionString;
    private static string? _jwtKey;
    private static int _jwtExpiryMinutes;

    private static async Task LogAction(long? userId, string action, IDbConnection db)
    {
        try
        {
            await db.ExecuteAsync(
                "INSERT INTO log (user_id, details) VALUES (@userId, @details)",
                new { userId, details = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}: {action}" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log action: {ex.Message}");
        }
    }
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
                            ?? throw new Exception("Connection string not found");
        
        var jwtSection = config.GetSection("Jwt");
        _jwtKey = jwtSection["Key"] 
                  ?? throw new Exception("Jwt:Key missing");
        _jwtExpiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var m) ? m : 60;

        var group = routes.MapGroup("/auth")
            .WithTags("Auth");

        
        group.MapPost("/register", async (RegisterRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.nick) || string.IsNullOrWhiteSpace(req.password))
                return Results.BadRequest("Nick and Password required.");

            var hash = BCrypt.Net.BCrypt.HashPassword(req.password);

            using IDbConnection db = new NpgsqlConnection(_connectionString);
            var existing = await db.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM \"user\" WHERE nick = @nick",
                new { nick = req.nick });
            if (existing is not null)
                return Results.Conflict("User already exists.");

            var insertSql = "INSERT INTO \"user\"(nick, password_hash, created_at) VALUES (@nick, @hash, now()) RETURNING id;";
            var newId = await db.ExecuteScalarAsync<long>(insertSql, new { nick = req.nick, hash });

            await LogAction(newId, $"User registered with nick: {req.nick}", db);

            return Results.Created();
        });

        
        group.MapPost("/login", async (LoginRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.nick) || string.IsNullOrWhiteSpace(req.password))
                return Results.BadRequest("Nick and Password required.");

            using IDbConnection db = new NpgsqlConnection(_connectionString);
            var user = await db.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM \"user\" WHERE nick = @nick",
                new { nick = req.nick });

            if (user is null)
                return Results.NotFound("User not found");

            var passwordHash = user.password_hash;
            if (string.IsNullOrEmpty(passwordHash) || !BCrypt.Net.BCrypt.Verify(req.password, passwordHash))
                return Results.BadRequest($"{req.password}_{passwordHash}_{user.created_at}_{user.id}");

            await LogAction(user.id, $"User logged in", db);

            var token = GenerateJwt(user);

            return Results.Ok(new { token });
        });

        return routes;
    }
    
    
    private static string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.id.ToString()),
            new Claim(ClaimTypes.Name, user.nick),
            new Claim(ClaimTypes.Role, user.is_admin ? "Admin" : "User")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public class RegisterRequest
    {
        public string nick { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string nick { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }
}