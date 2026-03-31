namespace EnterpriseApp.Domain.Common;

/// <summary>
/// Base entity providing common audit fields and concurrency control via RowVersion.
/// All domain entities should inherit from this class.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Gets or sets the unique identifier for this entity.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>Gets or sets the UTC timestamp when the entity was created.</summary>
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp when the entity was last modified.</summary>
    public DateTime? UpdatedAtUtc { get; protected set; }

    /// <summary>Gets or sets the row version used for optimistic concurrency control.</summary>
    public byte[] RowVersion { get; protected set; } = Array.Empty<byte>();

    /// <summary>Marks the entity as modified by updating the timestamp.</summary>
    public void MarkAsUpdated()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
