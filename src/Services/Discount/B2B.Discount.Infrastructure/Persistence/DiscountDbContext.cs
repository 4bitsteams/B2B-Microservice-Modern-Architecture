using Microsoft.EntityFrameworkCore;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Infrastructure.Persistence;

public sealed class DiscountDbContext(DbContextOptions<DiscountDbContext> options, IServiceProvider serviceProvider)
    : BaseDbContext(options, serviceProvider)
{
    public DbSet<DiscountEntity> Discounts => Set<DiscountEntity>();
    public DbSet<Coupon> Coupons => Set<Coupon>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DiscountEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).IsRequired().HasMaxLength(FieldLengths.Name);
            b.Property(e => e.Value).HasPrecision(18, 4);
            b.Property(e => e.MinimumOrderAmount).HasPrecision(18, 2);
            b.HasIndex(e => new { e.TenantId, e.IsActive });
        });

        modelBuilder.Entity<Coupon>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Code).IsRequired().HasMaxLength(FieldLengths.Code);
            b.HasIndex(e => new { e.Code, e.TenantId }).IsUnique();
            b.Property(e => e.Name).IsRequired().HasMaxLength(FieldLengths.Name);
            b.Property(e => e.Value).HasPrecision(18, 4);
            b.Property(e => e.MinimumOrderAmount).HasPrecision(18, 2);
            b.HasIndex(e => new { e.TenantId, e.IsActive });
        });
    }
}
