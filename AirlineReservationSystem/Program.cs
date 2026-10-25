using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AirlineReservationSystem.Helpers;
using AirlineReservationSystem.Middlewares;
using Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repository;
using Service;
using Service.Helpers.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Enums travel as text ("Economy", "Confirmed"), the way the frontend uses them.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// Sign-in and register are limited per IP address (Auth:RateLimitPerMinute) against password guessing and account spam.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
    {
        var limit = Math.Max(1, httpContext.RequestServices.GetRequiredService<IOptions<AuthSettings>>().Value.RateLimitPerMinute);
        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    options.AddPolicy(RateLimitPolicies.Lookup, httpContext =>
    {
        var limit = Math.Max(1, httpContext.RequestServices.GetRequiredService<IOptions<AuthSettings>>().Value.LookupRateLimitPerMinute);
        return RateLimitPartition.GetFixedWindowLimiter(
            "lookup:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Too many requests from this address. Try again in a minute.",
            Instance = context.HttpContext.Request.Path
        };
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
    };
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
builder.Services.AddRepositoryLayer(connectionString);
builder.Services.AddServiceLayer(builder.Configuration);

// The signing key lives in user-secrets (development) or the environment; refuse to start without a usable one.
builder.Services.AddOptions<JwtSettings>()
    .Validate(settings => Encoding.UTF8.GetByteCount(settings.Secret ?? string.Empty) >= 32,
        "Jwt:Secret is missing or shorter than 32 bytes. Set it with dotnet user-secrets.")
    .ValidateOnStart();

// Tokens are validated with the same settings TokenService signs them with.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwt) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // A valid signature is not enough: the account must still exist, be active and have the role the token says.
        options.Events = new JwtBearerEvents { OnTokenValidated = TokenStateValidator.ValidateAsync };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(RoleNames.Admin, RoleNames.SuperAdmin));
    options.AddPolicy(AuthorizationPolicies.SuperAdmin, policy => policy.RequireRole(RoleNames.SuperAdmin));
});
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AEROVA API", Version = "v1" });
    options.AddSecurityDefinition(AuthorizeOperationFilter.SchemeName, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken returned by POST /api/auth/login."
    });
    options.OperationFilter<AuthorizeOperationFilter>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Empty 401/403/404 responses get a problem details body too.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// In Development the browser pages call http://localhost:5080 directly; a redirect to https would fail their CORS preflight requests.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Development admin account (Seed:CreateAdmin) and demo data (Seed:Enabled): Development only, off by default (see DemoSeedStartup).
await app.SeedDemoDataIfEnabledAsync();

app.Run();

// Lets the integration tests start the API with WebApplicationFactory<Program>.
public partial class Program
{
}
