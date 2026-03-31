using EnterpriseApp.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EnterpriseApp.API.Middleware;

/// <summary>
/// Global exception handling middleware. Catches all unhandled exceptions and maps them
/// to appropriate HTTP status codes with a uniform response envelope.
/// Internal exception details are never exposed to the client.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Initializes a new instance of <see cref="GlobalExceptionMiddleware"/>.</summary>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Processes the HTTP request and catches any unhandled exceptions.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        var (statusCode, message) = exception switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
            NotFoundException nfe  => (StatusCodes.Status404NotFound, nfe.Message),
            UnauthorizedException  => (StatusCodes.Status401Unauthorized, "Unauthorized."),
            DomainException de     => (StatusCodes.Status400BadRequest, de.Message),
            ConcurrencyException   => (StatusCodes.Status409Conflict, "The resource was modified by another request. Please retry."),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request was cancelled."),
            _                      => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please contact support.")
        };

        // Log at Error for 5xx, Warning for 4xx
        if (statusCode >= 500)
            _logger.LogError(exception, "Unhandled exception. TraceId={TraceId} Path={Path}", traceId, context.Request.Path);
        else
            _logger.LogWarning(exception, "Handled exception {ExceptionType}. TraceId={TraceId}", exception.GetType().Name, traceId);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new
        {
            success = false,
            message,
            data = (object?)null,
            traceId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
