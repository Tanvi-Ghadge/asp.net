using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseApp.Application.Services;

/// <summary>
/// Implements JWT authentication with refresh token rotation.
/// Secrets are retrieved from <see cref="ISecretProvider"/>, never from config files.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ISecretProvider _secretProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    private const int AccessTokenExpiryMinutes = 15;
    private const int RefreshTokenExpiryDays = 7;

    /// <summary>Initializes a new instance of <see cref="AuthService"/>.</summary>
    public AuthService(
        IRefreshTokenRepository refreshTokenRepository,
        ISecretProvider secretProvider,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _secretProvider = secretProvider;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        // NOTE: In production, validate against a user store. Simplified for demonstration.
        if (request.Username != "admin" || request.Password != "Admin@12345")
            throw new UnauthorizedException("Invalid credentials.");

        _logger.LogInformation("User {Username} authenticated successfully", request.Username);

        var userId = Guid.NewGuid(); // Replace with real user lookup
        return await GenerateTokensAsync(userId, request.Username, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = ComputeHash(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken)
            ?? throw new UnauthorizedException("Refresh token is invalid or expired.");

        if (!storedToken.IsActive)
            throw new UnauthorizedException("Refresh token has been revoked.");

        // Token rotation: revoke old, issue new
        storedToken.Revoke();
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotated for user {UserId}", storedToken.UserId);

        return await GenerateTokensAsync(storedToken.UserId, storedToken.UserId.ToString(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = ComputeHash(refreshToken);
        var storedToken = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken)
            ?? throw new NotFoundException(nameof(RefreshToken), "token");

        storedToken.Revoke();
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token revoked for user {UserId}", storedToken.UserId);
    }

    private async Task<TokenResponse> GenerateTokensAsync(Guid userId, string username, CancellationToken cancellationToken)
    {
        var jwtSecret = await _secretProvider.GetSecretAsync("JwtSecret", cancellationToken);
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        // Generate opaque refresh token
        var rawRefreshToken = GenerateSecureToken();
        var tokenHash = ComputeHash(rawRefreshToken);
        var refreshTokenEntity = RefreshToken.Create(userId, tokenHash, DateTime.UtcNow.AddDays(RefreshTokenExpiryDays));
        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new TokenResponse(accessToken, rawRefreshToken, expiry);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
