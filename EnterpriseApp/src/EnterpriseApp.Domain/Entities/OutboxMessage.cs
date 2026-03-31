namespace EnterpriseApp.Domain.Entities;

/// <summary>Represents an outbox message used in the Outbox Pattern for reliable distributed messaging.</summary>
public sealed class OutboxMessage
{
    /// <summary>Gets the unique identifier for the outbox message.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Gets the message type (fully qualified or short name).</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Gets the serialized payload of the domain event.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when the message was created.</summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>Gets the UTC timestamp when the message was processed (null if pending).</summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>Gets the error message if processing failed.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets the number of times processing was attempted.</summary>
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    /// <summary>Factory method to create a new OutboxMessage.</summary>
    public static OutboxMessage Create(string type, string payload)
    {
        return new OutboxMessage { Type = type, Payload = payload };
    }

    /// <summary>Marks the message as successfully processed.</summary>
    public void MarkAsProcessed()
    {
        ProcessedAtUtc = DateTime.UtcNow;
        ErrorMessage = null;
    }

    /// <summary>Marks the message as failed with the given error detail.</summary>
    public void MarkAsFailed(string error)
    {
        ErrorMessage = error;
        RetryCount++;
    }
}
