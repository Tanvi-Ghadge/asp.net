using EnterpriseApp.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Infrastructure.Outbox;

/// <summary>
/// Background service that polls the OutboxMessages table and publishes pending events.
/// Uses the Outbox Pattern to guarantee at-least-once delivery without distributed transactions.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    /// <summary>Initializes a new instance of <see cref="OutboxProcessor"/>.</summary>
    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred while processing outbox messages.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor stopped.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return;

        _logger.LogInformation("Processing {Count} outbox messages.", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                // Dispatch to message broker (e.g., RabbitMQ, Azure Service Bus)
                // Replace this with your actual broker publish call
                await DispatchEventAsync(message.Type, message.Payload, cancellationToken);

                message.MarkAsProcessed();
                _logger.LogInformation("Outbox message {MessageId} of type {Type} dispatched.", message.Id, message.Type);
            }
            catch (Exception ex)
            {
                message.MarkAsFailed(ex.Message);
                _logger.LogError(ex, "Failed to dispatch outbox message {MessageId} of type {Type}.", message.Id, message.Type);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task DispatchEventAsync(string type, string payload, CancellationToken cancellationToken)
    {
        // TODO: Replace with actual broker integration (RabbitMQ / Azure Service Bus / Kafka)
        _logger.LogInformation("Dispatching event Type={Type} Payload={Payload}", type, payload);
        return Task.CompletedTask;
    }
}
