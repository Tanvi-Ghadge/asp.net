using EnterpriseApp.Application.Interfaces;
using EnterpriseApp.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace EnterpriseApp.Infrastructure.Persistence.Context;

/// <summary>
/// Adapter that exposes only the operations the Application layer needs from EF Core,
/// without depending on Infrastructure's concrete DbContext type.
/// </summary>
public sealed class AppDbContextAdapter : IAppDbContext
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="AppDbContextAdapter"/>.</summary>
    public AppDbContextAdapter(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        => _context.Database.BeginTransactionAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _context.SaveChangesAsync(cancellationToken);
}

