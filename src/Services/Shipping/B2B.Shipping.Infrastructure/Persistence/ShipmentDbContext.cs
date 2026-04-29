using Microsoft.EntityFrameworkCore;
using B2B.Shipping.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;
using ShipmentEntity = B2B.Shipping.Domain.Entities.Shipment;

namespace B2B.Shipping.Infrastructure.Persistence;

public sealed class ShipmentDbContext(DbContextOptions<ShipmentDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<ShipmentEntity> Shipments => Set<ShipmentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ShipmentEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TrackingNumber).IsRequired().HasMaxLength(100);
            b.HasIndex(e => e.TrackingNumber).IsUnique();
            b.HasIndex(e => e.OrderId);
            b.HasIndex(e => new { e.TenantId, e.Status });
            b.Property(e => e.Carrier).IsRequired().HasMaxLength(100);
            b.Property(e => e.RecipientName).IsRequired().HasMaxLength(200);
            b.Property(e => e.ShippingAddress).IsRequired().HasMaxLength(300);
            b.Property(e => e.City).IsRequired().HasMaxLength(100);
            b.Property(e => e.Country).IsRequired().HasMaxLength(3);
            b.Property(e => e.ShippingCost).HasPrecision(18, 2);
            b.Property(e => e.EstimatedDelivery).HasMaxLength(50);
        });
    }
}
