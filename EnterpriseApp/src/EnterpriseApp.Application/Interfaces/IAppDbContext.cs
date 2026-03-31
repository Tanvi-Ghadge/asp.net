using EnterpriseApp.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace EnterpriseApp.Application.Interfaces;

/// <summary>
/// Small abstraction over the EF Core DbContext so the Application layer doesn't depend
/// on Infrastructure's concrete EF Core context type.
/// </summary>
public interface IAppDbContext
{
    /// <summary>Begins a database transaction for the current unit of work.</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>Adds an outbox message (to be dispatched by the outbox processor).</summary>
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>Saves all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

