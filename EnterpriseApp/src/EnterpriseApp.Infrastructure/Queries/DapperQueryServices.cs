using Dapper;
using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Infrastructure.Queries;

/// <summary>
/// Dapper-based READ-ONLY query service for Products.
/// NEVER uses EF Core — raw SQL only via Dapper.
/// </summary>
public sealed class ProductQueryService : IProductQueryService
{
    private readonly string _connectionString;
    private readonly ILogger<ProductQueryService> _logger;

    /// <summary>Initializes a new instance of <see cref="ProductQueryService"/>.</summary>
    public ProductQueryService(IConfiguration configuration, ILogger<ProductQueryService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dapper: Querying product {ProductId}", id);

        const string sql = """
            SELECT Id, Name, Description, Price, StockQuantity, IsActive, CreatedAtUtc
            FROM Products
            WHERE Id = @Id AND IsActive = 1
            """;

        await using var connection = new SqlConnection(_connectionString);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var result = await connection.QueryFirstOrDefaultAsync<ProductReadModel>(command);

        return result is null ? null : MapToDto(result);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dapper: Querying products page {Page} size {PageSize}", page, pageSize);

        const string sql = """
            SELECT Id, Name, Description, Price, StockQuantity, IsActive, CreatedAtUtc
            FROM Products
            WHERE IsActive = 1
            ORDER BY CreatedAtUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        await using var connection = new SqlConnection(_connectionString);
        var command = new CommandDefinition(
            sql,
            new { Offset = (page - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken);

        var results = await connection.QueryAsync<ProductReadModel>(command);
        return results.Select(MapToDto).ToList().AsReadOnly();
    }

    private static ProductDto MapToDto(ProductReadModel r) =>
        new(r.Id, r.Name, r.Description, r.Price, r.StockQuantity, r.IsActive, r.CreatedAtUtc);

    // Internal Dapper read model — keeps EF entities out of the read path
    private sealed record ProductReadModel(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        int StockQuantity,
        bool IsActive,
        DateTime CreatedAtUtc
    );
}

/// <summary>
/// Dapper-based READ-ONLY query service for Orders.
/// NEVER uses EF Core — raw SQL only via Dapper.
/// </summary>
public sealed class OrderQueryService : IOrderQueryService
{
    private readonly string _connectionString;
    private readonly ILogger<OrderQueryService> _logger;

    /// <summary>Initializes a new instance of <see cref="OrderQueryService"/>.</summary>
    public OrderQueryService(IConfiguration configuration, ILogger<OrderQueryService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dapper: Querying order {OrderId}", id);

        const string sql = """
            SELECT o.Id, o.CustomerId, o.Status, o.TotalAmount, o.CreatedAtUtc,
                   i.ProductId, i.ProductName, i.Quantity, i.UnitPrice
            FROM Orders o
            INNER JOIN OrderItems i ON i.OrderId = o.Id
            WHERE o.Id = @Id
            """;

        await using var connection = new SqlConnection(_connectionString);

        OrderDto? order = null;
        var items = new List<OrderItemDto>();

        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);

        await connection.QueryAsync<OrderReadModel, OrderItemReadModel, OrderDto>(
            command,
            (o, i) =>
            {
                order ??= new OrderDto(o.Id, o.CustomerId, o.Status, o.TotalAmount, o.CreatedAtUtc, items);
                items.Add(new OrderItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice));
                return order;
            },
            splitOn: "ProductId");

        return order;
    }

    private sealed record OrderReadModel(Guid Id, Guid CustomerId, string Status, decimal TotalAmount, DateTime CreatedAtUtc);
    private sealed record OrderItemReadModel(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
}
