using System.Text.Json;
using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Application.Services;

/// <summary>
/// Implements order placement with multi-table transactions (Order + Product stock + Outbox)
/// using EF Core transactions with a single SaveChanges call.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderQueryService _orderQueryService;
    private readonly IAppDbContext _appDbContext;
    private readonly ILogger<OrderService> _logger;

    /// <summary>Initializes a new instance of <see cref="OrderService"/>.</summary>
    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IOrderQueryService orderQueryService,
        IAppDbContext appDbContext,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _orderQueryService = orderQueryService;
        _appDbContext = appDbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Placing order for customer {CustomerId}", request.CustomerId);

        // Begin EF Core transaction — covers Order, Product stock, and Outbox atomically
        await using var transaction = await _appDbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = Order.Create(request.CustomerId);

            foreach (var item in request.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Product), item.ProductId);

                product.DeductStock(item.Quantity);
                _productRepository.Update(product);

                order.AddItem(product.Id, product.Name, item.Quantity, product.Price);
            }

            order.Confirm();
            await _orderRepository.AddAsync(order, cancellationToken);

            // Outbox: record domain event in same transaction — no distributed transaction needed
            var eventPayload = JsonSerializer.Serialize(new
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                TotalAmount = order.TotalAmount
            });
            var outboxMessage = OutboxMessage.Create("OrderPlaced", eventPayload);
            await _appDbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);

            // Single SaveChanges for all tables — atomic commit
            await _appDbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} placed successfully, total {TotalAmount}", order.Id, order.TotalAmount);

            return await _orderQueryService.GetByIdAsync(order.Id, cancellationToken)
                ?? throw new InvalidOperationException("Order was saved but could not be retrieved.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to place order for customer {CustomerId}. Transaction rolled back.", request.CustomerId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<OrderDto?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving order {OrderId}", id);
        return await _orderQueryService.GetByIdAsync(id, cancellationToken);
    }
}
