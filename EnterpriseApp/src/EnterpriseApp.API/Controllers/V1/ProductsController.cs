using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using FluentValidation;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.API.Controllers.V1;

/// <summary>
/// Products API — version 1.
/// Provides CRUD operations for the product catalog.
/// All write operations use EF Core; all reads use Dapper via the query service.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
[Authorize]
[Produces("application/json")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<UpdateProductRequest> _updateValidator;
    private readonly ILogger<ProductsController> _logger;

    /// <summary>Initializes a new instance of <see cref="ProductsController"/>.</summary>
    public ProductsController(
        IProductService productService,
        IValidator<CreateProductRequest> createValidator,
        IValidator<UpdateProductRequest> updateValidator,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    /// <summary>Returns a paged list of active products.</summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 200 — paged product list.
    /// 401 — unauthenticated.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        _logger.LogInformation("GetProducts called. Page={Page} PageSize={PageSize}", page, pageSize);

        var products = await _productService.GetProductsAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductDto>>.Ok(products, traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>Returns a single product by its unique identifier.</summary>
    /// <param name="id">The product GUID.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 200 — product found.
    /// 404 — product not found.
    /// 401 — unauthenticated.
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProduct(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetProduct called for {ProductId}", id);

        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound(ApiResponse<ProductDto>.Fail($"Product {id} not found.", HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ProductDto>.Ok(product, traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>Creates a new product in the catalog.</summary>
    /// <param name="request">Product creation payload.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 201 — product created, Location header set.
    /// 400 — validation failure.
    /// 401 — unauthenticated.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<ProductDto>.Fail(errors, HttpContext.TraceIdentifier));
        }

        var product = await _productService.CreateProductAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetProduct),
            new { id = product.Id },
            ApiResponse<ProductDto>.Ok(product, "Product created successfully.", HttpContext.TraceIdentifier));
    }

    /// <summary>Updates an existing product's name, description, and price.</summary>
    /// <param name="id">The product GUID to update.</param>
    /// <param name="request">Updated product payload.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 200 — product updated.
    /// 400 — validation failure.
    /// 404 — product not found.
    /// 409 — concurrency conflict.
    /// 401 — unauthenticated.
    /// </returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(errors, HttpContext.TraceIdentifier));
        }

        await _productService.UpdateProductAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Product updated successfully.", HttpContext.TraceIdentifier));
    }
}
