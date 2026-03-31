using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace EnterpriseApp.API.Middleware;

/// <summary>
/// Enriches every request with a Correlation ID and logs request start/end with timing.
/// The correlation ID is taken from the incoming X-Correlation-ID header or generated fresh.
/// It is pushed into Serilog's LogContext so all downstream log entries carry it.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    /// <summary>Initializes a new instance of <see cref="CorrelationIdMiddleware"/>.</summary>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Processes the request, injecting and propagating the correlation ID.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId)
            ? existingId.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Push into Serilog context so all log entries in this request include CorrelationId
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Request started {Method} {Path}", context.Request.Method, context.Request.Path);

            await _next(context);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation(
                "Request completed {Method} {Path} {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                elapsed);
        }
    }
}
