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
/// Orders API — version 1.
/// Handles order placement with multi-table transaction support (Order + Stock + Outbox).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Authorize]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IValidator<PlaceOrderRequest> _placeOrderValidator;
    private readonly ILogger<OrdersController> _logger;

    /// <summary>Initializes a new instance of <see cref="OrdersController"/>.</summary>
    public OrdersController(
        IOrderService orderService,
        IValidator<PlaceOrderRequest> placeOrderValidator,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _placeOrderValidator = placeOrderValidator;
        _logger = logger;
    }

    /// <summary>Places a new order for a customer, updating stock atomically.</summary>
    /// <param name="request">Order placement payload containing customer ID and line items.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 201 — order created.
    /// 400 — validation failure or insufficient stock.
    /// 401 — unauthenticated.
    /// 404 — one or more products not found.
    /// 500 — unexpected error.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _placeOrderValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<OrderDto>.Fail(errors, HttpContext.TraceIdentifier));
        }

        _logger.LogInformation("PlaceOrder for customer {CustomerId}", request.CustomerId);

        var order = await _orderService.PlaceOrderAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.Id },
            ApiResponse<OrderDto>.Ok(order, "Order placed successfully.", HttpContext.TraceIdentifier));
    }

    /// <summary>Retrieves a single order by its unique identifier.</summary>
    /// <param name="id">The order GUID.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 200 — order found.
    /// 404 — order not found.
    /// 401 — unauthenticated.
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetOrder called for {OrderId}", id);

        var order = await _orderService.GetOrderByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound(ApiResponse<OrderDto>.Fail($"Order {id} not found.", HttpContext.TraceIdentifier));

        return Ok(ApiResponse<OrderDto>.Ok(order, traceId: HttpContext.TraceIdentifier));
    }
}
