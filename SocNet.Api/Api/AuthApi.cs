using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SocNet.Api.Entities;
using SocNet.Api.Mongo; 

namespace SocNet.Api.Api;

public static class AuthApi
{
    private static string? _jwtKey;
    private static int _jwtExpiryMinutes;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes, IConfiguration config)
    {
        var jwtSection = config.GetSection("Jwt");
        _jwtKey = jwtSection["Key"] ?? throw new Exception("Jwt:Key missing");
        _jwtExpiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var m) ? m : 60;

        var group = routes.MapGroup("/auth")
            .WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest req, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new AuthApiLogged(cfg, cache, logService);
            
            if (string.IsNullOrWhiteSpace(req.nick) || string.IsNullOrWhiteSpace(req.password))
                return Results.BadRequest("Nick and Password required.");

            var hash = BCrypt.Net.BCrypt.HashPassword(req.password);

            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(null, $"SELECT user by nick: {req.nick}");
            var existing = await db.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM \"user\" WHERE nick = @nick",
                new { nick = req.nick });
            
            if (existing is not null)
                return Results.Conflict("User already exists.");

            var insertSql = "INSERT INTO \"user\"(nick, password_hash, created_at) VALUES (@nick, @hash, now()) RETURNING id;";
            
            await loggedApi.LogDbQuery(null, $"INSERT new user with nick {req.nick}");
            var newId = await db.ExecuteScalarAsync<long>(insertSql, new { nick = req.nick, hash });

            await loggedApi.LogAction(newId, $"User registered with nick: {req.nick}");

            return Results.Created();
        });

        group.MapPost("/login", async (LoginRequest req, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new AuthApiLogged(cfg, cache, logService);

            if (string.IsNullOrWhiteSpace(req.nick) || string.IsNullOrWhiteSpace(req.password))
                return Results.BadRequest("Nick and Password required.");
            
            var blacklistKey = $"blacklist:{req.nick}"; 
            var blockedTime = await cache.GetStringAsync(blacklistKey);
            if (blockedTime != null)
                return Results.BadRequest("Account blacklisted for 10 minutes.");
            
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            
            await loggedApi.LogDbQuery(null, $"SELECT user by nick: {req.nick}");
            var user = await db.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM \"user\" WHERE nick = @nick",
                new { nick = req.nick });

            if (user is null)
                return Results.NotFound("User not found");

            bool isBadPassword = false;
            try
            {
                var passwordHash = user.password_hash;
                isBadPassword = string.IsNullOrEmpty(passwordHash) ||
                                !BCrypt.Net.BCrypt.Verify(req.password, passwordHash);
            }
            catch (Exception ex)
            {
                await loggedApi.LogException(user.id, ex);
                isBadPassword = true;
            }
            
            if (isBadPassword)
            {
                string attemptsKey = $"attempts:{req.nick}";
                var attemptsStr = await cache.GetStringAsync(attemptsKey);
                int attempts = attemptsStr == null ? 0 : int.Parse(attemptsStr);
                attempts++;
                
                if (attempts >= 3)
                {
                    await cache.SetStringAsync(blacklistKey, DateTime.UtcNow.ToString(), 
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
                    await cache.RemoveAsync(attemptsKey);
                    
                    await loggedApi.LogAction(user.id, "Account blacklisted due to multiple failed login attempts");
                    return Results.BadRequest("Too many attempts. Blocked for 10 minutes.");
                }
                
                await cache.SetStringAsync(attemptsKey, attempts.ToString(), 
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
                
                return Results.BadRequest($"Wrong password. Remaining attempts: {3 - attempts}");
            }

            await loggedApi.LogAction(user.id, "User logged in");
            await cache.RemoveAsync($"attempts:{req.nick}");
            
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

    private class AuthApiLogged : LoggedApi
    {
        public AuthApiLogged(IConfiguration config, IDistributedCache cache, MongoLogService logService) 
            : base(config, cache, logService) { }
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