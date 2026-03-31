namespace EnterpriseApp.Domain.Exceptions;

/// <summary>Thrown when a requested domain resource is not found.</summary>
public sealed class NotFoundException : Exception
{
    /// <summary>Initializes a NotFoundException for the specified entity and key.</summary>
    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.") { }
}

/// <summary>Thrown when a concurrency conflict is detected during a write operation.</summary>
public sealed class ConcurrencyException : Exception
{
    /// <summary>Initializes a ConcurrencyException with a message.</summary>
    public ConcurrencyException(string message) : base(message) { }
}

/// <summary>Thrown when a business rule or domain invariant is violated.</summary>
public sealed class DomainException : Exception
{
    /// <summary>Initializes a DomainException with a descriptive message.</summary>
    public DomainException(string message) : base(message) { }
}

/// <summary>Thrown when the caller is not authorized to perform an operation.</summary>
public sealed class UnauthorizedException : Exception
{
    /// <summary>Initializes an UnauthorizedException with a message.</summary>
    public UnauthorizedException(string message) : base(message) { }
}
