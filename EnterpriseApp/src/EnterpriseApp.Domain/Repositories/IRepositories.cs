using EnterpriseApp.Domain.Entities;

namespace EnterpriseApp.Domain.Repositories;

/// <summary>Write-only repository interface for Product entities (EF Core).</summary>
public interface IProductRepository
{
    /// <summary>Adds a new product to the store.</summary>
    Task AddAsync(Product product, CancellationToken cancellationToken);

    /// <summary>Updates an existing product in the store.</summary>
    void Update(Product product);

    /// <summary>Retrieves a product by its identifier using EF Core tracking.</summary>
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Saves all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Write-only repository interface for Order entities (EF Core).</summary>
public interface IOrderRepository
{
    /// <summary>Adds a new order.</summary>
    Task AddAsync(Order order, CancellationToken cancellationToken);

    /// <summary>Retrieves an order with its items by identifier.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Saves all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Write-only repository interface for RefreshToken entities (EF Core).</summary>
public interface IRefreshTokenRepository
{
    /// <summary>Adds a new refresh token.</summary>
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>Retrieves an active refresh token by its hash.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Saves all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
