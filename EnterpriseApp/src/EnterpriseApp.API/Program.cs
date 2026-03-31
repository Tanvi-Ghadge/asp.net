using EnterpriseApp.API.Extensions;
using EnterpriseApp.API.Middleware;
using EnterpriseApp.Infrastructure.Caching;
using EnterpriseApp.Infrastructure.Logging;
using EnterpriseApp.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System.IO.Compression;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog (structured JSON logging, level-controlled via appsettings) ──────
builder.Host.UseEnterpriseLogging();

// ── Core MVC ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── API Versioning ────────────────────────────────────────────────────────────
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EnterpriseApp API",
        Version = "v1",
        Description = "Production-grade ASP.NET Core enterprise API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments from all projects
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
    foreach (var xmlFile in xmlFiles)
        options.IncludeXmlComments(xmlFile);
});

// ── Application layers ────────────────────────────────────────────────────────
builder.Services
    .AddDatabase(builder.Configuration)
    .AddRepositories()
    .AddQueryServices()
    .AddApplicationServices()
    .AddValidators()
    .AddRedis(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration)
    .AddBlobStorage(builder.Configuration)
    .AddSecretProvider(builder.Configuration)
    .AddOutboxProcessor()
    .AddEnterpriseHealthChecks(builder.Configuration);

// ── GZip Compression ──────────────────────────────────────────────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ratelimitingpolicy", context =>
{
    var ip = context.Connection.RemoteIpAddress?.ToString();

    return RateLimitPartition.GetFixedWindowLimiter(
        ip!,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
});
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

// The app registers a DbContext via `.AddDatabase(...)`, but the database/tables
// might not exist yet (first run with empty SQL Server). Ensure the schema exists
// early so background services + `/health` don't fail with "Cannot open database".
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// ── Middleware pipeline (ORDER MATTERS) ───────────────────────────────────────

// 1. Response compression
app.UseResponseCompression();

// 2. Correlation ID + request logging (must be first to cover all downstream logs)
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. Global exception handler (catches everything below)
app.UseMiddleware<GlobalExceptionMiddleware>();

// 4. Redis-backed rate limiting
//rate-limiting

// app.UseMiddleware<RedisRateLimitingMiddleware>();

// 5. HMAC signature validation (before JWT, for machine-to-machine endpoints)
app.UseMiddleware<HmacAuthenticationMiddleware>();

// 6. CORS
app.UseCors("DefaultPolicy");

// 7. HTTPS redirect
app.UseHttpsRedirection();
app.UseRateLimiter();
// 8. Authentication + Authorization
app.UseAuthentication();
app.UseAuthorization();

// 9. Swagger (dev only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "EnterpriseApp API v1");
    });
}

// 10. Controllers
app.MapControllers();

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
