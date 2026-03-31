using System.Security.Cryptography;
using System.Text;
using EnterpriseApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.API.Middleware;

/// <summary>
/// Validates HMAC-SHA256 signatures on incoming requests.
/// Clients must include:
///   X-Api-Key   — the client's public API key
///   X-Timestamp — Unix timestamp (UTC) within 5-minute window
///   X-Signature — HMAC-SHA256(method + path + timestamp + body, sharedSecret)
/// This prevents replay attacks and message tampering.
/// </summary>
public sealed class HmacAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HmacAuthenticationMiddleware> _logger;
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Initializes a new instance of <see cref="HmacAuthenticationMiddleware"/>.</summary>
    public HmacAuthenticationMiddleware(RequestDelegate next, ILogger<HmacAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Validates the HMAC signature on the request.</summary>
    public async Task InvokeAsync(HttpContext context, ISecretProvider secretProvider)
    {
        // Skip HMAC for public endpoints
        if (IsPublicEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var apiKey)
            || !context.Request.Headers.TryGetValue("X-Timestamp", out var timestampHeader)
            || !context.Request.Headers.TryGetValue("X-Signature", out var signature))
        {
            _logger.LogWarning("HMAC headers missing on request {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Missing HMAC headers.", traceId = context.TraceIdentifier });
            return;
        }

        if (!long.TryParse(timestampHeader, out var timestamp))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Invalid timestamp.", traceId = context.TraceIdentifier });
            return;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalSeconds) > TimestampTolerance.TotalSeconds)
        {
            _logger.LogWarning("HMAC timestamp replay attempt from {ApiKey}", apiKey.ToString());
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Request timestamp expired.", traceId = context.TraceIdentifier });
            return;
        }

        // Buffer request body for signature computation
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        var sharedSecret = await secretProvider.GetSecretAsync($"HmacSecret:{apiKey}", CancellationToken.None);
        var expectedSignature = ComputeHmac(
            $"{context.Request.Method}{context.Request.Path}{timestamp}{body}",
            sharedSecret);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature.ToString()),
                Encoding.UTF8.GetBytes(expectedSignature)))
        {
            _logger.LogWarning("HMAC signature mismatch for API key {ApiKey} on {Path}", apiKey, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Invalid signature.", traceId = context.TraceIdentifier });
            return;
        }

        await _next(context);
    }

    private static string ComputeHmac(string message, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var hashBytes = HMACSHA256.HashData(keyBytes, messageBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool IsPublicEndpoint(PathString path)
    {
        return path.StartsWithSegments("/health")
            || path.StartsWithSegments("/api/v1/auth")
            || path.StartsWithSegments("/swagger");
    }
}
