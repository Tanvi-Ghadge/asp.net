using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using FluentValidation;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.RateLimiting;

namespace EnterpriseApp.API.Controllers.V1;

/// <summary>
/// Authentication API — version 1.
/// Handles JWT login, refresh token rotation, and token revocation.
/// These endpoints are excluded from HMAC auth and rate limiting.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly ILogger<AuthController> _logger;

    /// <summary>Initializes a new instance of <see cref="AuthController"/>.</summary>
    public AuthController(
        IAuthService authService,
        IValidator<LoginRequest> loginValidator,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _loginValidator = loginValidator;
        _logger = logger;
    }

    /// <summary>Authenticates a user and returns a JWT access token plus a refresh token.</summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 200 — tokens issued.
    /// 400 — validation failure.
    /// 401 — invalid credentials.
    /// </returns>
    /// 
    [EnableRateLimiting("ratelimitingpolicy")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<TokenResponse>.Fail(errors, HttpContext.TraceIdentifier));
        }

        // Sensitive fields must never appear in logs
        _logger.LogInformation("Login attempt for user {Username}", request.Username);

        var tokens = await _authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<TokenResponse>.Ok(tokens, "Login successful.", HttpContext.TraceIdentifier));
    }

    /// <summary>Issues a new access token using a valid refresh token (token rotation).</summary>
    /// <param name="request">Refresh token payload.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 200 — new tokens issued, old refresh token revoked.
    /// 401 — invalid or expired refresh token.
    /// </returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(ApiResponse<TokenResponse>.Ok(tokens, "Token refreshed.", HttpContext.TraceIdentifier));
    }

    /// <summary>Revokes a refresh token, preventing it from being used again.</summary>
    /// <param name="request">Refresh token to revoke.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 200 — token revoked.
    /// 404 — token not found.
    /// </returns>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Token revoked.", HttpContext.TraceIdentifier));
    }
}
