using System.Text.Json;
using EnterpriseApp.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EnterpriseApp.Infrastructure.Caching;

/// <summary>
/// Redis-backed distributed cache implementation.
/// Uses StackExchange.Redis for connection management and JSON serialization for values.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initializes a new instance of <see cref="RedisCacheService"/>.</summary>
    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return null;

            // `RedisValue` can implicitly convert to both `string` and `ReadOnlySpan<byte>`,
            // so we force the `string` overload to avoid ambiguity.
            var valueString = value.ToString();
            return JsonSerializer.Deserialize<T>(valueString, SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis GET failed for key {CacheKey}", key);
            return null; // Graceful degradation — cache miss on Redis failure
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value, SerializerOptions);
            await _database.StringSetAsync(key, serialized, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis SET failed for key {CacheKey}", key);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis DELETE failed for key {CacheKey}", key);
        }
    }
}
