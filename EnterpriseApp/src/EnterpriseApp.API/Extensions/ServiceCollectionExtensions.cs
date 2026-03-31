using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using EnterpriseApp.Application.Services;
using EnterpriseApp.Application.Validators;
using EnterpriseApp.Domain.Repositories;
using EnterpriseApp.Infrastructure.Caching;
using EnterpriseApp.Infrastructure.Outbox;
using EnterpriseApp.Infrastructure.Persistence.Context;
using EnterpriseApp.Infrastructure.Persistence.Repositories;
using EnterpriseApp.Infrastructure.Queries;
using EnterpriseApp.Infrastructure.Security;
using EnterpriseApp.Infrastructure.Storage;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

namespace EnterpriseApp.API.Extensions;

/// <summary>Extension methods that register all application services in <c>Program.cs</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers EF Core DbContext.</summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));

        // Expose a small DbContext abstraction to the Application layer.
        services.AddScoped<IAppDbContext, AppDbContextAdapter>();

        return services;
    }

    /// <summary>Registers all domain repositories (EF Core — write only).</summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        return services;
    }

    /// <summary>Registers all Dapper read-only query services.</summary>
    public static IServiceCollection AddQueryServices(this IServiceCollection services)
    {
        services.AddScoped<IProductQueryService, ProductQueryService>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();
        return services;
    }

    /// <summary>Registers all application services.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }

    /// <summary>Registers FluentValidation validators.</summary>
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
        services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
        services.AddScoped<IValidator<PlaceOrderRequest>, PlaceOrderRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        return services;
    }

    /// <summary>Registers Redis distributed cache and rate limiter.</summary>
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is required.");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddScoped<ICacheService, RedisCacheService>();
        return services;
    }

    /// <summary>Registers JWT Bearer authentication.</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    // Key is fetched from Key Vault at startup via ISecretProvider in AuthService
                    IssuerSigningKeyResolver = (_, _, _, _) =>
                    {
                        var jwtSecret = configuration["Jwt:SigningKey"]
                            ?? throw new InvalidOperationException("JWT signing key not configured.");
                        return new[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)) };
                    },
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        return services;
    }

    /// <summary>Registers Azure Blob Storage client.</summary>
    public static IServiceCollection AddBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AzureStorage")
            ?? throw new InvalidOperationException("Azure Storage connection string is required.");

        services.AddSingleton(_ => new BlobServiceClient(connectionString));
        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        return services;
    }

    /// <summary>Registers Azure Key Vault secret provider.</summary>
    public static IServiceCollection AddSecretProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var vaultUri = configuration["KeyVault:Uri"];
        var useKeyVault =
            !string.IsNullOrWhiteSpace(vaultUri)
            && !vaultUri.Contains("your-keyvault", StringComparison.OrdinalIgnoreCase);

        if (useKeyVault)
        {
            services.AddSingleton(_ => new SecretClient(new Uri(vaultUri!), new DefaultAzureCredential()));
            services.AddSingleton<ISecretProvider, AzureKeyVaultSecretProvider>();
        }
        else
        {
            // Local dev fallback (env vars / appsettings / docker-compose environment).
            services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
        }

        return services;
    }

    /// <summary>Registers the Outbox background processor.</summary>
    public static IServiceCollection AddOutboxProcessor(this IServiceCollection services)
    {
        services.AddHostedService<OutboxProcessor>();
        return services;
    }

    /// <summary>Registers health checks for SQL Server and Redis.</summary>
    public static IServiceCollection AddEnterpriseHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "sqlserver",
                tags: new[] { "database" })
            .AddRedis(
                configuration.GetConnectionString("Redis")!,
                name: "redis",
                tags: new[] { "cache" });

        return services;
    }
}
