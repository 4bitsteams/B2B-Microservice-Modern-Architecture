using Microsoft.EntityFrameworkCore;
using B2B.Shared.Infrastructure.Persistence;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Infrastructure.Persistence;

public sealed class VendorDbContext(DbContextOptions<VendorDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<VendorEntity> Vendors => Set<VendorEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VendorEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.CompanyName).IsRequired().HasMaxLength(300);
            b.Property(e => e.ContactEmail).IsRequired().HasMaxLength(256);
            b.HasIndex(e => new { e.ContactEmail, e.TenantId }).IsUnique();
            b.Property(e => e.TaxId).IsRequired().HasMaxLength(50);
            b.HasIndex(e => new { e.TaxId, e.TenantId }).IsUnique();
            b.Property(e => e.Address).IsRequired().HasMaxLength(300);
            b.Property(e => e.City).IsRequired().HasMaxLength(100);
            b.Property(e => e.Country).IsRequired().HasMaxLength(3);
            b.Property(e => e.Website).HasMaxLength(500);
            b.Property(e => e.Description).HasMaxLength(2000);
            b.Property(e => e.CommissionRate).HasPrecision(5, 2);
            b.HasIndex(e => new { e.TenantId, e.Status });
        });
    }
}
