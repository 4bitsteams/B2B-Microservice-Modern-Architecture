using Microsoft.EntityFrameworkCore;
using B2B.Product.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Product.Infrastructure.Persistence;

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options, IServiceProvider serviceProvider)
    : BaseDbContext(options, serviceProvider)
{
    public DbSet<Domain.Entities.Product> Products => Set<Domain.Entities.Product>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).IsRequired().HasMaxLength(FieldLengths.Name);
            b.Property(e => e.Slug).IsRequired().HasMaxLength(FieldLengths.Slug);
            b.HasIndex(e => new { e.Slug, e.TenantId }).IsUnique();
            b.HasOne(e => e.ParentCategory).WithMany(c => c.SubCategories)
                .HasForeignKey(e => e.ParentCategoryId).IsRequired(false);
        });

        modelBuilder.Entity<Domain.Entities.Product>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).IsRequired().HasMaxLength(FieldLengths.LongName);
            b.Property(e => e.Sku).IsRequired().HasMaxLength(FieldLengths.Sku);
            b.HasIndex(e => new { e.Sku, e.TenantId }).IsUnique();

            // Value object - Money
            b.OwnsOne(e => e.Price, m =>
            {
                m.Property(p => p.Amount).HasColumnName("price").HasPrecision(18, 2);
                m.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(FieldLengths.CurrencyCode);
            });
            b.OwnsOne(e => e.CompareAtPrice, m =>
            {
                m.Property(p => p.Amount).HasColumnName("compare_at_price").HasPrecision(18, 2);
                m.Property(p => p.Currency).HasColumnName("compare_at_currency").HasMaxLength(FieldLengths.CurrencyCode);
            });

            b.HasOne(e => e.Category).WithMany(c => c.Products).HasForeignKey(e => e.CategoryId);

            // Row-level tenant filter
            b.HasQueryFilter(p => p.Status != Domain.Entities.ProductStatus.Archived);
        });
    }
}
