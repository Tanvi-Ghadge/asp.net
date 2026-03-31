using EnterpriseApp.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Infrastructure.Security;

/// <summary>
/// Local-development secret provider backed by configuration/environment variables.
/// This allows the app to run without Azure Key Vault.
/// </summary>
public sealed class ConfigurationSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationSecretProvider> _logger;

    /// <summary>Initializes a new instance of <see cref="ConfigurationSecretProvider"/>.</summary>
    public ConfigurationSecretProvider(IConfiguration configuration, ILogger<ConfigurationSecretProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        // Map known secret names to config keys.
        // - AuthService asks for "JwtSecret"
        // - Hmac middleware asks for $"HmacSecret:{apiKey}"
        string? value = secretName switch
        {
            "JwtSecret" => _configuration["Jwt:SigningKey"],
            _ when secretName.StartsWith("HmacSecret:", StringComparison.OrdinalIgnoreCase)
                => _configuration[$"Hmac:Secrets:{secretName["HmacSecret:".Length..]}"],
            _ => _configuration[secretName]
        };

        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogError("Missing secret {SecretName} in configuration.", secretName);
            throw new InvalidOperationException($"Missing secret '{secretName}'. Configure it (local dev) or set up Azure Key Vault.");
        }

        return Task.FromResult(value);
    }
}

