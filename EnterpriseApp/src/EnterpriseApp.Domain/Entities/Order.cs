using EnterpriseApp.Domain.Common;

namespace EnterpriseApp.Domain.Entities;

/// <summary>Represents an order placed by a customer.</summary>
public sealed class Order : BaseEntity
{
    private readonly List<OrderItem> _items = new();

    private Order() { }

    /// <summary>Gets the customer identifier.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Gets the order status.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    /// <summary>Gets the total amount for the order.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Gets the read-only collection of order items.</summary>
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>Factory method to create a new Order.</summary>
    public static Order Create(Guid customerId)
    {
        return new Order { CustomerId = customerId };
    }

    /// <summary>Adds an item to the order and recalculates the total.</summary>
    public void AddItem(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        _items.Add(OrderItem.Create(Id, productId, productName, quantity, unitPrice));
        TotalAmount = _items.Sum(i => i.TotalPrice);
        MarkAsUpdated();
    }

    /// <summary>Marks the order as confirmed.</summary>
    public void Confirm()
    {
        Status = OrderStatus.Confirmed;
        MarkAsUpdated();
    }
}

/// <summary>Represents a single line item within an order.</summary>
public sealed class OrderItem : BaseEntity
{
    private OrderItem() { }

    /// <summary>Gets the parent order identifier.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the product identifier.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Gets the product name at time of order.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Gets the quantity ordered.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the unit price at time of order.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Gets the total price for this line item.</summary>
    public decimal TotalPrice => Quantity * UnitPrice;

    internal static OrderItem Create(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice)
    {
        return new OrderItem
        {
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
    }
}


/// <summary>Represents the lifecycle status of an order.</summary>
public enum OrderStatus
{
    /// <summary>Order is created but not yet confirmed.</summary>
    Pending = 0,

    /// <summary>Order has been confirmed.</summary>
    Confirmed = 1,

    /// <summary>Order has been shipped.</summary>
    Shipped = 2,

    /// <summary>Order has been delivered to the customer.</summary>
    Delivered = 3,

    /// <summary>Order has been cancelled.</summary>
    Cancelled = 4
}
