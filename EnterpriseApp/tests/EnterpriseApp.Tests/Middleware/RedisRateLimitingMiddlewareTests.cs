using EnterpriseApp.Infrastructure.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Net;
using System.Text.Json;

namespace EnterpriseApp.Tests.Middleware;

[TestFixture]
public sealed class RedisRateLimitingMiddlewareTests
{
    private Mock<IConnectionMultiplexer> _redisMock = null!;
    private Mock<IDatabase> _databaseMock = null!;
    private Mock<ILogger<RedisRateLimitingMiddleware>> _loggerMock = null!;
    private IConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisRateLimitingMiddleware>>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:RequestsPerWindow"] = "2",
                ["RateLimit:WindowSeconds"] = "60"
            })
            .Build();

        _redisMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);
    }

    [Test]
    public async Task InvokeAsync_WhenLimitExceeded_ShouldReturn429()
    {
        var counts = new Queue<long>(new long[] { 1, 2, 3 });
        _databaseMock
            .Setup(x => x.StringIncrementAsync("rate_limit:loopback", It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() => counts.Dequeue());
        _databaseMock
            .Setup(x => x.KeyExpireAsync("rate_limit:loopback", It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var nextCalled = false;
        var middleware = new RedisRateLimitingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            _redisMock.Object,
            _configuration,
            _loggerMock.Object);

        await middleware.InvokeAsync(CreateContext(IPAddress.Loopback));
        await middleware.InvokeAsync(CreateContext(IPAddress.Loopback));
        var blockedContext = CreateContext(IPAddress.Loopback);

        await middleware.InvokeAsync(blockedContext);

        Assert.That(blockedContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));
        Assert.That(nextCalled, Is.True);
        Assert.That(blockedContext.Response.Headers["X-RateLimit-Limit"].ToString(), Is.EqualTo("2"));
        Assert.That(blockedContext.Response.Headers["Retry-After"].ToString(), Is.EqualTo("60"));

        blockedContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(blockedContext.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body).RootElement;
        Assert.That(json.GetProperty("success").GetBoolean(), Is.False);

        _databaseMock.Verify(
            x => x.KeyExpireAsync("rate_limit:loopback", It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Test]
    public async Task InvokeAsync_ShouldPreferForwardedHeaderForClientKey()
    {
        _databaseMock
            .Setup(x => x.StringIncrementAsync("rate_limit:198.51.100.10", It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);
        _databaseMock
            .Setup(x => x.KeyExpireAsync("rate_limit:198.51.100.10", It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var middleware = new RedisRateLimitingMiddleware(
            _ => Task.CompletedTask,
            _redisMock.Object,
            _configuration,
            _loggerMock.Object);

        var context = CreateContext(IPAddress.Parse("10.0.0.5"));
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.10, 10.0.0.5";

        await middleware.InvokeAsync(context);

        _databaseMock.Verify(
            x => x.StringIncrementAsync("rate_limit:198.51.100.10", It.IsAny<long>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    private static DefaultHttpContext CreateContext(IPAddress remoteIpAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIpAddress;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
