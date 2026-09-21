using System.Text;
using System.Threading.RateLimiting;
using BKeeper.Infrastructure;
using BKeeper.Infrastructure.Auth;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(opt => opt.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
{
    In = Microsoft.OpenApi.ParameterLocation.Header,
    Description = "Paste a JWT: Bearer {token}",
    Name = "Authorization",
    Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
}));

builder.Services.AddBKeeperInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        // Without this, ASP.NET Core silently remaps the "role" (and "sub"/"email") claim to its
        // long-form ClaimTypes.* URI on every inbound token — so `User.FindFirst("role")` reads as
        // written in JwtTokenService return null, even though the claim is right there in the JWT.
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();

// §11 hardening: throttle login/bootstrap per client IP to blunt credential-stuffing/brute-force.
// ponytail: in-memory fixed-window limiter, single instance — fine for one API replica; a
// multi-instance deploy needs a shared store (e.g. Redis) instead.
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 10 }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BKeeperDbContext>();
    db.Database.Migrate();
}

// Swagger is exposed in every environment (including the deployed Azure Web App) so the
// API contract stays browsable at /swagger without needing ASPNETCORE_ENVIRONMENT=Development.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();

// Scopes the request (and every EF query in it) to the box_id carried in the caller's JWT.
app.Use(async (context, next) =>
{
    var boxClaim = context.User.FindFirst(JwtTokenService.BoxIdClaim)?.Value;
    if (Guid.TryParse(boxClaim, out var boxId))
    {
        var accessor = context.RequestServices.GetRequiredService<CurrentBoxAccessor>();
        using (accessor.Use(boxId))
        {
            await next();
            return;
        }
    }
    await next();
});

app.UseAuthorization();

app.MapControllers();
app.MapHangfireDashboard("/hangfire").RequireAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

public partial class Program; // exposed for WebApplicationFactory in integration tests
