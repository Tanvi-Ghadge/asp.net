using EnterpriseApp.Application.DTOs;

namespace EnterpriseApp.Application.Interfaces;

/// <summary>Service interface for product operations.</summary>
public interface IProductService
{
    /// <summary>Creates a new product and returns its read model.</summary>
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken);

    /// <summary>Updates an existing product.</summary>
    Task UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);

    /// <summary>Retrieves a product by identifier.</summary>
    Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns a paged list of active products.</summary>
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>Service interface for order operations including multi-table transactions.</summary>
public interface IOrderService
{
    /// <summary>Places a new order, deducting stock and recording an outbox event atomically.</summary>
    Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Retrieves an order by its identifier.</summary>
    Task<OrderDto?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Service interface for JWT authentication and token management.</summary>
public interface IAuthService
{
    /// <summary>Authenticates a user and returns access/refresh tokens.</summary>
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Rotates a refresh token and issues a new access token.</summary>
    Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    /// <summary>Revokes a refresh token, preventing further use.</summary>
    Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken);
}

/// <summary>Read-only query interface for products using Dapper.</summary>
public interface IProductQueryService
{
    /// <summary>Returns a product read model by identifier using a raw SQL read path.</summary>
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns a paged list of products using a raw SQL read path.</summary>
    Task<IReadOnlyList<ProductDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>Read-only query interface for orders using Dapper.</summary>
public interface IOrderQueryService
{
    /// <summary>Returns an order with its line items using a raw SQL read path.</summary>
    Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Blob storage abstraction for file uploads.</summary>
public interface IBlobStorageService
{
    /// <summary>Streams a file directly to blob storage without buffering in memory.</summary>
    Task<string> UploadStreamAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);

    /// <summary>Deletes a blob by its URL or name.</summary>
    Task DeleteAsync(string blobUrl, CancellationToken cancellationToken);
}

/// <summary>Cache service abstraction backed by Redis.</summary>
public interface ICacheService
{
    /// <summary>Gets a cached value by key, or null if not present.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class;

    /// <summary>Sets a value in cache with the specified expiry.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken) where T : class;

    /// <summary>Removes a cached value by key.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Secret provider abstraction — retrieves secrets from a secure vault (e.g., Azure Key Vault, blob-based).</summary>
public interface ISecretProvider
{
    /// <summary>Retrieves a secret value by its name.</summary>
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken);
}
