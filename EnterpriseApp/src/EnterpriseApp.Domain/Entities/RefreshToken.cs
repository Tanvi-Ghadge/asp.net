namespace EnterpriseApp.Domain.Entities;

/// <summary>Represents a JWT refresh token for a user.</summary>
public sealed class RefreshToken
{
    /// <summary>Gets the unique identifier.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Gets the user identifier this token belongs to.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the hashed token value.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Gets the UTC expiry date.</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Gets whether the token has been revoked.</summary>
    public bool IsRevoked { get; private set; }

    /// <summary>Gets the UTC time when the token was created.</summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private RefreshToken() { }

    /// <summary>Factory method to create a new RefreshToken.</summary>
    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    /// <summary>Returns true if the token is still valid.</summary>
    public bool IsActive => !IsRevoked && ExpiresAtUtc > DateTime.UtcNow;

    /// <summary>Revokes this refresh token to prevent reuse.</summary>
    public void Revoke()
    {
        IsRevoked = true;
    }
}
