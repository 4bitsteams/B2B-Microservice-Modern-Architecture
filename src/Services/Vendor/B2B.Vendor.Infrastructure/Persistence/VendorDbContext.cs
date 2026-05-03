using Microsoft.EntityFrameworkCore;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Infrastructure.Persistence;

public sealed class VendorDbContext(DbContextOptions<VendorDbContext> options, IServiceProvider serviceProvider)
    : BaseDbContext(options, serviceProvider)
{
    public DbSet<VendorEntity> Vendors => Set<VendorEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VendorEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.CompanyName).IsRequired().HasMaxLength(FieldLengths.LongName);
            b.Property(e => e.ContactEmail).IsRequired().HasMaxLength(FieldLengths.Email);
            b.HasIndex(e => new { e.ContactEmail, e.TenantId }).IsUnique();
            b.Property(e => e.TaxId).IsRequired().HasMaxLength(FieldLengths.TaxId);
            b.HasIndex(e => new { e.TaxId, e.TenantId }).IsUnique();
            b.Property(e => e.Address).IsRequired().HasMaxLength(FieldLengths.AddressLine);
            b.Property(e => e.City).IsRequired().HasMaxLength(FieldLengths.City);
            b.Property(e => e.Country).IsRequired().HasMaxLength(FieldLengths.CountryCode);
            b.Property(e => e.Website).HasMaxLength(FieldLengths.Url);
            b.Property(e => e.Description).HasMaxLength(FieldLengths.Description);
            b.Property(e => e.CommissionRate).HasPrecision(5, 2);
            b.HasIndex(e => new { e.TenantId, e.Status });
        });
    }
}
