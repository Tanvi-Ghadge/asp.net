using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Repositories;
using EnterpriseApp.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProductRepository"/>.
/// WRITE operations ONLY — reads must use the Dapper query service.
/// </summary>
public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="ProductRepository"/>.</summary>
    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        await _context.Products.AddAsync(product, cancellationToken);
    }

    /// <inheritdoc/>
    public void Update(Product product)
    {
        _context.Products.Update(product);
    }

    /// <inheritdoc/>
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Tracking query — needed for write operations and concurrency detection
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of <see cref="IOrderRepository"/>.
/// WRITE operations ONLY — reads must use the Dapper query service.
/// </summary>
public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="OrderRepository"/>.</summary>
    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of <see cref="IRefreshTokenRepository"/>.
/// WRITE operations ONLY.
/// </summary>
public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="RefreshTokenRepository"/>.</summary>
    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        await _context.RefreshTokens.AddAsync(token, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
