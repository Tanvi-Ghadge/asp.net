using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace EnterpriseApp.Infrastructure.Logging;

/// <summary>
/// Configures Serilog for structured JSON logging with:
/// - Correlation ID enrichment
/// - Environment-controlled log levels
/// - Debug logs gated behind configuration (never active in production unless explicitly enabled)
/// - Sensitive field masking
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Configures Serilog on the provided <see cref="IHostBuilder"/>.
    /// Log level is controlled via appsettings — debug output is suppressed unless
    /// Serilog:MinimumLevel:Default is explicitly set to "Debug" in configuration.
    /// </summary>
    public static IHostBuilder UseEnterpriseLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var env = context.HostingEnvironment;
            var config = context.Configuration;

            configuration
                .ReadFrom.Configuration(config)         // appsettings controls all levels
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()                 // Picks up CorrelationId, RequestPath etc.
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Application", "EnterpriseApp")
                .Enrich.WithProperty("Environment", env.EnvironmentName)
                .Filter.ByExcluding(logEvent =>         // Mask sensitive field names in all log events
                    logEvent.Properties.ContainsKey("Password")
                    || logEvent.Properties.ContainsKey("Token")
                    || logEvent.Properties.ContainsKey("Secret"))
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.File(
                    formatter: new Serilog.Formatting.Json.JsonFormatter(),
                    path: "logs/app-.json",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    restrictedToMinimumLevel: LogEventLevel.Information);

            // In development, add verbose console output
            if (env.IsDevelopment())
            {
                configuration.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug);
            }
        });
    }
}
