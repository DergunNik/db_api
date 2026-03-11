using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SocNet.Api.Entities;
using SocNet.Api.Mongo;
using StackExchange.Redis;

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
            
            var existing = await db.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM \"user\" WHERE nick = @nick",
                new { nick = req.nick });
            
            if (existing is not null)
                return Results.Conflict("User already exists.");

            var insertSql = "INSERT INTO \"user\"(nick, password_hash, created_at) VALUES (@nick, @hash, now()) RETURNING id;";
            var newId = await db.ExecuteScalarAsync<long>(insertSql, new { nick = req.nick, hash });

            await loggedApi.LogAction(newId, $"User registered: {req.nick}");
            return Results.Created();
        });

        group.MapPost("/login", async (LoginRequest req, IDistributedCache cache, IConfiguration cfg, MongoLogService logService) =>
        {
            var loggedApi = new AuthApiLogged(cfg, cache, logService);

            var blacklistKey = $"blacklist:{req.nick}"; 
            if (await cache.GetStringAsync(blacklistKey) != null)
                return Results.BadRequest("Account blacklisted for 10 minutes.");
            
            using IDbConnection db = new NpgsqlConnection(loggedApi.ConnectionString);
            var user = await db.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM \"user\" WHERE nick = @nick",
                new { nick = req.nick });

            if (user is null) return Results.NotFound("User not found");

            if (!BCrypt.Net.BCrypt.Verify(req.password, user.password_hash))
            {
                string attemptsKey = $"attempts:{req.nick}";
                var attemptsStr = await cache.GetStringAsync(attemptsKey);
                int attempts = (attemptsStr == null ? 0 : int.Parse(attemptsStr)) + 1;
                
                if (attempts >= 3)
                {
                    await cache.SetStringAsync(blacklistKey, "blocked", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
                    await cache.RemoveAsync(attemptsKey);
                    return Results.BadRequest("Blocked for 10 minutes.");
                }
                await cache.SetStringAsync(attemptsKey, attempts.ToString(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
                return Results.BadRequest($"Wrong password. Remaining: {3 - attempts}");
            }

            await cache.RemoveAsync($"attempts:{req.nick}");

            var (token, jti) = GenerateJwtWithJti(user);

            var sessionKey = $"session:{user.id}:{jti}";
            await cache.SetStringAsync(sessionKey, "active", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_jwtExpiryMinutes)
            });

            await loggedApi.LogAction(user.id, "User logged in with session creation");
            return Results.Ok(new { token });
        });

        group.MapPost("/logout", async (
            ClaimsPrincipal principal, 
            IDistributedCache cache, 
            IMemoryCache memoryCache,
            IConnectionMultiplexer redis,
            IConfiguration cfg, 
            MongoLogService logService) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);

            if (userId != null && jti != null)
            {
                var sessionKey = $"session:{userId}:{jti}";

                await cache.RemoveAsync(sessionKey);
                
                memoryCache.Remove(sessionKey);

                var subscriber = redis.GetSubscriber();
                await subscriber.PublishAsync("session-revoked", sessionKey);
                
                var loggedApi = new AuthApiLogged(cfg, cache, logService);
                await loggedApi.LogAction(long.Parse(userId), "User logged out (session invalidated)");
            }

            return Results.Ok("Logged out successfully");
        }).RequireAuthorization();

        return routes;
    }

    private static (string Token, string Jti) GenerateJwtWithJti(User user)
    {
        var jti = Guid.NewGuid().ToString(); 
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(ClaimTypes.Name, user.nick),
            new Claim(ClaimTypes.Role, user.is_admin ? "Admin" : "User")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtExpiryMinutes),
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), jti);
    }

    private class AuthApiLogged : LoggedApi
    {
        public AuthApiLogged(IConfiguration config, IDistributedCache cache, MongoLogService logService) 
            : base(config, cache, logService) { }
    }
    
    public record RegisterRequest(string nick, string password);
    public record LoginRequest(string nick, string password);
}