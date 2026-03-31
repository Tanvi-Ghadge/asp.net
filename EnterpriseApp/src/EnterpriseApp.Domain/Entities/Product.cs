using EnterpriseApp.Domain.Common;

namespace EnterpriseApp.Domain.Entities;

/// <summary>
/// Represents a product in the catalog.
/// </summary>
public sealed class Product : BaseEntity
{
    private Product() { } // Required by EF Core

    /// <summary>Gets the product name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the product description.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the product price.</summary>
    public decimal Price { get; private set; }

    /// <summary>Gets the available stock quantity.</summary>
    public int StockQuantity { get; private set; }

    /// <summary>Gets whether the product is active.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Creates a new Product instance using factory method.
    /// </summary>
    /// <param name="name">Product name.</param>
    /// <param name="description">Product description.</param>
    /// <param name="price">Product price.</param>
    /// <param name="stockQuantity">Initial stock quantity.</param>
    public static Product Create(string name, string description, decimal price, int stockQuantity)
    {
        return new Product
        {
            Name = name,
            Description = description,
            Price = price,
            StockQuantity = stockQuantity
        };
    }

    /// <summary>Updates product details.</summary>
    public void Update(string name, string description, decimal price)
    {
        Name = name;
        Description = description;
        Price = price;
        MarkAsUpdated();
    }

    /// <summary>Decrements stock by the given quantity.</summary>
    public void DeductStock(int quantity)
    {
        if (StockQuantity < quantity)
            throw new InvalidOperationException($"Insufficient stock for product {Id}.");
        StockQuantity -= quantity;
        MarkAsUpdated();
    }

    /// <summary>Deactivates the product.</summary>
    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }
}
