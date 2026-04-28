using Microsoft.EntityFrameworkCore;
using B2B.Order.Application.Sagas;
using B2B.Order.Infrastructure.Persistence.Sagas;
using B2B.Shared.Infrastructure.Persistence;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderItemEntity = B2B.Order.Domain.Entities.OrderItem;

namespace B2B.Order.Infrastructure.Persistence;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();

    /// <summary>
    /// Saga state rows — one per in-flight order fulfillment workflow.
    /// Rows are deleted automatically when the saga finalizes (<c>SetCompletedWhenFinalized</c>).
    /// </summary>
    public DbSet<OrderFulfillmentSagaState> OrderFulfillmentSagas => Set<OrderFulfillmentSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrderEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            b.HasIndex(e => e.OrderNumber).IsUnique();
            b.HasIndex(e => new { e.CustomerId, e.TenantId });
            b.HasIndex(e => new { e.TenantId, e.Status });

            b.OwnsOne(e => e.ShippingAddress, addr =>
            {
                addr.Property(a => a.Street).HasColumnName("shipping_street").HasMaxLength(300);
                addr.Property(a => a.City).HasColumnName("shipping_city").HasMaxLength(100);
                addr.Property(a => a.State).HasColumnName("shipping_state").HasMaxLength(100);
                addr.Property(a => a.PostalCode).HasColumnName("shipping_postal_code").HasMaxLength(20);
                addr.Property(a => a.Country).HasColumnName("shipping_country").HasMaxLength(3);
            });

            b.OwnsOne(e => e.BillingAddress, addr =>
            {
                addr.Property(a => a.Street).HasColumnName("billing_street").HasMaxLength(300);
                addr.Property(a => a.City).HasColumnName("billing_city").HasMaxLength(100);
                addr.Property(a => a.State).HasColumnName("billing_state").HasMaxLength(100);
                addr.Property(a => a.PostalCode).HasColumnName("billing_postal_code").HasMaxLength(20);
                addr.Property(a => a.Country).HasColumnName("billing_country").HasMaxLength(3);
            });

            b.HasMany(e => e.Items).WithOne().HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItemEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.ProductName).IsRequired().HasMaxLength(300);
            b.Property(e => e.Sku).IsRequired().HasMaxLength(100);
            b.Property(e => e.UnitPrice).HasPrecision(18, 2);
        });

        // Apply saga state configuration via IEntityTypeConfiguration
        modelBuilder.ApplyConfiguration(new OrderFulfillmentSagaStateMap());
    }
}
