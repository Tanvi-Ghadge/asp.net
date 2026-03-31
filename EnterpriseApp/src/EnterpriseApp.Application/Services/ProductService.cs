using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Application.Services;

/// <summary>
/// Implements product management operations. Write operations use EF Core via
/// <see cref="IProductRepository"/>; reads are delegated to the Dapper-based
/// <see cref="IProductQueryService"/>.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IProductQueryService _productQueryService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ProductService> _logger;

    private const string CacheKeyPrefix = "product:";

    /// <summary>
    /// Initializes a new instance of <see cref="ProductService"/>.
    /// </summary>
    public ProductService(
        IProductRepository productRepository,
        IProductQueryService productQueryService,
        ICacheService cacheService,
        ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _productQueryService = productQueryService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc/>
    // public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    // {
    //     _logger.LogInformation("Creating product with name {ProductName}", request.Name);

    //     var product = Product.Create(request.Name, request.Description, request.Price, request.StockQuantity);
    //     await _productRepository.AddAsync(product, cancellationToken);
    //     await _productRepository.SaveChangesAsync(cancellationToken);

    //     _logger.LogInformation("Product created successfully with Id {ProductId}", product.Id);

    //     return MapToDto(product);
    // }

public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
{
    _logger.LogInformation(">>> STEP 1: Service started for product {ProductName}", request.Name);

    // TODO: Remove this delay after cancellation token testing is complete
    await Task.Delay(8000, cancellationToken);

    _logger.LogInformation(">>> STEP 2: After delay — about to hit DB");

    var product = Product.Create(request.Name, request.Description, request.Price, request.StockQuantity);
    await _productRepository.AddAsync(product, cancellationToken);
    await _productRepository.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(">>> STEP 3: DB write done — product {ProductId} saved", product.Id);

    return MapToDto(product);
}
    /// <inheritdoc/>
    public async Task UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating product {ProductId}", id);

        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);

        product.Update(request.Name, request.Description, request.Price);
        _productRepository.Update(product);

        try
        {
            await _productRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException($"Product {id} was modified by another request. Please retry.");
        }

        await _cacheService.RemoveAsync($"{CacheKeyPrefix}{id}", cancellationToken);

        _logger.LogInformation("Product {ProductId} updated successfully", id);
    }

    /// <inheritdoc/>
    public async Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}{id}";
        var cached = await _cacheService.GetAsync<ProductDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogInformation("Product {ProductId} served from cache", id);
            return cached;
        }

        // Reads go through Dapper query service
        var product = await _productQueryService.GetByIdAsync(id, cancellationToken);
        if (product is not null)
        {
            await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromMinutes(10), cancellationToken);
        }

        return product;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching products page {Page} size {PageSize}", page, pageSize);
        // Reads use Dapper query service
        return await _productQueryService.GetPagedAsync(page, pageSize, cancellationToken);
    }

    private static ProductDto MapToDto(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.IsActive, p.CreatedAtUtc);
}
