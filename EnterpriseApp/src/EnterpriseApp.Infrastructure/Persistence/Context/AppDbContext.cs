using EnterpriseApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Persistence.Context;

/// <summary>
/// EF Core DbContext for write operations only. All reads should go through Dapper query services.
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <summary>Initializes a new instance of <see cref="AppDbContext"/>.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>Products write set.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Orders write set.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Order items write set.</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <summary>Outbox messages for distributed event publishing.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Refresh tokens for JWT rotation.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Product ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).IsRequired().HasMaxLength(2000);
            entity.Property(p => p.Price).HasPrecision(18, 4);
            // Optimistic concurrency via RowVersion
            entity.Property(p => p.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(p => p.Name);
        });

        // ── Order ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 4);
            entity.Property(o => o.Status).HasConversion<int>();
            entity.Property(o => o.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        });

        // ── OrderItem ────────────────────────────────────────────────────────
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(i => i.UnitPrice).HasPrecision(18, 4);
        });

        // ── OutboxMessage ────────────────────────────────────────────────────
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Type).IsRequired().HasMaxLength(500);
            entity.Property(m => m.Payload).IsRequired();
            entity.HasIndex(m => m.ProcessedAtUtc); // Efficient polling query
        });

        // ── RefreshToken ─────────────────────────────────────────────────────
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
            entity.HasIndex(t => t.TokenHash).IsUnique();
        });
    }
}
