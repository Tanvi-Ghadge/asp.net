// using Microsoft.AspNetCore.Http;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
// using StackExchange.Redis;
// using System.Net;

// namespace EnterpriseApp.Infrastructure.Caching;

// /// <summary>
// /// Redis-backed distributed rate limiting middleware using a fixed-window counter.
// /// Each client IP is tracked across all pods and proxies once normalized.
// /// </summary>
// public sealed class RedisRateLimitingMiddleware
// {
//     private readonly RequestDelegate _next;
//     private readonly IConnectionMultiplexer _redis;
//     private readonly ILogger<RedisRateLimitingMiddleware> _logger;
//     private readonly int _limit;
//     private readonly TimeSpan _window;

//     /// <summary>Initializes a new instance of <see cref="RedisRateLimitingMiddleware"/>.</summary>
//     public RedisRateLimitingMiddleware(
//         RequestDelegate next,
//         IConnectionMultiplexer redis,
//         IConfiguration configuration,
//         ILogger<RedisRateLimitingMiddleware> logger)
//     {
//         _next = next;
//         _redis = redis;
//         _logger = logger;
//         _limit = configuration.GetValue("RateLimit:RequestsPerWindow", 5);
//         _window = TimeSpan.FromSeconds(configuration.GetValue("RateLimit:WindowSeconds", 60));

//         _logger.LogInformation(
//             "Rate limiter initialized. Limit={Limit}, WindowSeconds={WindowSeconds}",
//             _limit,
//             _window.TotalSeconds);
//     }

//     /// <summary>Evaluates the rate limit for the incoming request.</summary>
//     public async Task InvokeAsync(HttpContext context)
//     {
//         var clientIdentifier = GetClientIdentifier(context);
//         var key = $"rate_limit:{clientIdentifier}";

//         try
//         {
//             var db = _redis.GetDatabase();
//             var count = await db.StringIncrementAsync(key);

//             if (count == 1)
//                 await db.KeyExpireAsync(key, _window);

//             if (count > _limit)
//             {
//                 _logger.LogWarning(
//                     "Rate limit exceeded for client {ClientIdentifier}. Count={Count}",
//                     clientIdentifier,
//                     count);

//                 context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
//                 context.Response.Headers["Retry-After"] = _window.TotalSeconds.ToString();
//                 context.Response.Headers["X-RateLimit-Limit"] = _limit.ToString();
//                 context.Response.Headers["X-RateLimit-Remaining"] = "0";

//                 await context.Response.WriteAsJsonAsync(new
//                 {
//                     success = false,
//                     message = "Too many requests. Please slow down.",
//                     traceId = context.TraceIdentifier
//                 });

//                 return;
//             }
//         }
//         catch (Exception ex)
//         {
//             // Redis unavailable: fail open to avoid blocking all traffic.
//             _logger.LogError(
//                 ex,
//                 "Redis rate limit check failed for {ClientIdentifier}. Allowing request.",
//                 clientIdentifier);
//         }

//         await _next(context);
//     }

//     private static string GetClientIdentifier(HttpContext context)
//     {
//         var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
//         var rawClientIp = !string.IsNullOrWhiteSpace(forwardedFor)
//             ? forwardedFor.Split(',')[0].Trim()
//             : context.Request.Headers["X-Real-IP"].FirstOrDefault();

//         if (string.IsNullOrWhiteSpace(rawClientIp))
//             rawClientIp = context.Connection.RemoteIpAddress?.ToString();

//         if (string.IsNullOrWhiteSpace(rawClientIp))
//             return "unknown";

//         if (!IPAddress.TryParse(rawClientIp, out var parsedIp))
//             return rawClientIp;

//         if (IPAddress.IsLoopback(parsedIp))
//             return "loopback";

//         return parsedIp.IsIPv4MappedToIPv6
//             ? parsedIp.MapToIPv4().ToString()
//             : parsedIp.ToString();
//     }
// }
