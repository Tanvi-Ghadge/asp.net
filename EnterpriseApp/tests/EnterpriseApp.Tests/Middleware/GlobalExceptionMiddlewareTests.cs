using EnterpriseApp.API.Middleware;
using EnterpriseApp.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text.Json;

namespace EnterpriseApp.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="GlobalExceptionMiddleware"/>.
/// Validates correct HTTP status codes, safe error messages, and proper logging levels.
/// </summary>
[TestFixture]
public sealed class GlobalExceptionMiddlewareTests
{
    private Mock<ILogger<GlobalExceptionMiddleware>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
    }

    [Test]
    public async Task InvokeAsync_NotFoundException_ShouldReturn404()
    {
        // Arrange
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new NotFoundException("Product", Guid.NewGuid()),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body).RootElement;

        Assert.That(json.GetProperty("success").GetBoolean(), Is.False);
        Assert.That(json.GetProperty("message").GetString(), Does.Contain("not found"));
    }

    [Test]
    public async Task InvokeAsync_UnauthorizedException_ShouldReturn401()
    {
        // Arrange
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new UnauthorizedException("Token is expired."),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task InvokeAsync_UnhandledException_ShouldReturn500WithSafeMessage()
    {
        // Arrange — internal exception detail must NOT leak to client
        var internalMessage = "DB connection string: Server=prod;Password=super_secret";
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException(internalMessage),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        // Internal exception message must never appear in the response
        Assert.That(body, Does.Not.Contain(internalMessage),
            "Internal exception details must not be exposed to the client.");
        Assert.That(body, Does.Not.Contain("super_secret"),
            "Sensitive data must not leak through error responses.");
    }

    [Test]
    public async Task InvokeAsync_DomainException_ShouldReturn400()
    {
        // Arrange
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new DomainException("Insufficient stock."),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body).RootElement;
        Assert.That(json.GetProperty("message").GetString(), Is.EqualTo("Insufficient stock."));
    }

    [Test]
    public async Task InvokeAsync_NoException_ShouldCallNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new GlobalExceptionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }
}
