using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using EnterpriseApp.Application.Services;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EnterpriseApp.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProductService"/>.
/// Verifies service logic, cache behaviour, and error propagation.
/// </summary>
[TestFixture]
public sealed class ProductServiceTests
{
    private Mock<IProductRepository> _productRepositoryMock = null!;
    private Mock<IProductQueryService> _productQueryServiceMock = null!;
    private Mock<ICacheService> _cacheServiceMock = null!;
    private Mock<ILogger<ProductService>> _loggerMock = null!;
    private ProductService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _productQueryServiceMock = new Mock<IProductQueryService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<ProductService>>();

        _sut = new ProductService(
            _productRepositoryMock.Object,
            _productQueryServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    // ── CreateProductAsync ────────────────────────────────────────────────────

    [Test]
    public async Task CreateProductAsync_ValidRequest_ShouldAddAndSave()
    {
        // Arrange
        var request = new CreateProductRequest("Widget", "A fine widget", 9.99m, 100);
        _productRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _productRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.CreateProductAsync(request, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo(request.Name));
        Assert.That(result.Price, Is.EqualTo(request.Price));
        Assert.That(result.IsActive, Is.True);

        _productRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _productRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateProductAsync_ShouldReturnDtoWithCorrectFields()
    {
        // Arrange
        var request = new CreateProductRequest("Gadget", "Description", 49.99m, 50);
        _productRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _productRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _sut.CreateProductAsync(request, CancellationToken.None);

        // Assert
        Assert.That(result.StockQuantity, Is.EqualTo(50));
        Assert.That(result.Id, Is.Not.EqualTo(Guid.Empty));
    }

    // ── GetProductByIdAsync ───────────────────────────────────────────────────

    [Test]
    public async Task GetProductByIdAsync_CacheHit_ShouldNotCallQueryService()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var cachedProduct = new ProductDto(productId, "Cached", "Desc", 10m, 5, true, DateTime.UtcNow);

        _cacheServiceMock
            .Setup(c => c.GetAsync<ProductDto>($"product:{productId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedProduct);

        // Act
        var result = await _sut.GetProductByIdAsync(productId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(cachedProduct));
        _productQueryServiceMock.Verify(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetProductByIdAsync_CacheMiss_ShouldCallQueryServiceAndSetCache()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productDto = new ProductDto(productId, "Fresh", "Desc", 20m, 10, true, DateTime.UtcNow);

        _cacheServiceMock
            .Setup(c => c.GetAsync<ProductDto>($"product:{productId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);

        _productQueryServiceMock
            .Setup(q => q.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(productDto);

        // Act
        var result = await _sut.GetProductByIdAsync(productId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(productDto));
        _cacheServiceMock.Verify(
            c => c.SetAsync($"product:{productId}", productDto, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task GetProductByIdAsync_NotFound_ShouldReturnNull()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _cacheServiceMock.Setup(c => c.GetAsync<ProductDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);
        _productQueryServiceMock.Setup(q => q.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);

        // Act
        var result = await _sut.GetProductByIdAsync(productId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    // ── UpdateProductAsync ────────────────────────────────────────────────────

    [Test]
    public async Task UpdateProductAsync_ProductNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var request = new UpdateProductRequest("New Name", "New Desc", 15m);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateProductAsync(productId, request, CancellationToken.None));
    }

    // ── Enforcement-style test: CancellationToken must propagate ─────────────

    [Test]
    public async Task CreateProductAsync_CancellationTokenMustBePassedToRepository()
    {
        // Arrange — use a specific token to verify it reaches the repository
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var request = new CreateProductRequest("T", "D", 1m, 1);

        CancellationToken capturedToken = default;
        _productRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((_, ct) => capturedToken = ct)
            .Returns(Task.CompletedTask);
        _productRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        await _sut.CreateProductAsync(request, token);

        // Assert — proves CancellationToken is passed all the way down
        Assert.That(capturedToken, Is.EqualTo(token),
            "CancellationToken must be passed from service to repository.");
    }
}
