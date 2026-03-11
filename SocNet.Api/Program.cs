using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using SocNet.Api.Api;
using SocNet.Api.Background;
using SocNet.Api.Mongo;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddMemoryCache();
builder.Services.AddHostedService<SessionInvalidationBackgroundService>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
    options.InstanceName = "SocNet:";
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis"))
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Введите JWT токен без префикса 'Bearer '"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var jwtSection = configuration.GetSection("Jwt");
var key = jwtSection["Key"] ?? throw new Exception("JWT Key missing");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev only
        options.SaveToken = true;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ValidateIssuer = false,
            ValidateAudience = false
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var distributedCache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                var memoryCache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

                if (userId != null && jti != null)
                {
                    var sessionKey = $"session:{userId}:{jti}";

                    if (!memoryCache.TryGetValue(sessionKey, out string? sessionActive))
                    {
                        sessionActive = await distributedCache.GetStringAsync(sessionKey);

                        if (!string.IsNullOrEmpty(sessionActive))
                        {
                            memoryCache.Set(sessionKey, sessionActive, TimeSpan.FromMinutes(5));
                        }
                    }

                    if (string.IsNullOrEmpty(sessionActive))
                    {
                        context.Fail("Session expired or revoked.");
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<MongoLogService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles(); 

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints(configuration);
app.MapUserEndpoints(configuration);
app.MapPostEndpoints(configuration);
app.MapSubscriptionEndpoints(configuration);
app.MapChatEndpoints(configuration);
app.MapReportEndpoints(configuration);
app.MapLogAdminEndpoints();
app.MapReportExportEndpoints();

app.Run();
