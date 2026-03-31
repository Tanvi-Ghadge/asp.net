using Azure.Security.KeyVault.Secrets;
using EnterpriseApp.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Infrastructure.Security;

/// <summary>
/// Azure Key Vault implementation of <see cref="ISecretProvider"/>.
/// All secrets (JWT signing keys, connection strings, etc.) are retrieved from Key Vault.
/// No secrets are stored in appsettings.json or environment variables.
/// </summary>
public sealed class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<AzureKeyVaultSecretProvider> _logger;

    // Simple in-process cache to avoid hitting Key Vault on every request
    private readonly Dictionary<string, (string Value, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>Initializes a new instance of <see cref="AzureKeyVaultSecretProvider"/>.</summary>
    public AzureKeyVaultSecretProvider(SecretClient secretClient, ILogger<AzureKeyVaultSecretProvider> logger)
    {
        _secretClient = secretClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(secretName, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                return cached.Value;

            _logger.LogInformation("Fetching secret {SecretName} from Key Vault.", secretName);

            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            var value = response.Value.Value;

            _cache[secretName] = (value, DateTime.UtcNow.Add(CacheDuration));
            return value;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
