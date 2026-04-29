using Microsoft.EntityFrameworkCore;
using B2B.Shared.Infrastructure.Persistence;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Infrastructure.Persistence;

public sealed class ReviewDbContext(DbContextOptions<ReviewDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<ReviewEntity> Reviews => Set<ReviewEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ReviewEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Title).IsRequired().HasMaxLength(200);
            b.Property(e => e.Body).IsRequired().HasMaxLength(2000);
            b.HasIndex(e => new { e.CustomerId, e.ProductId }).IsUnique();
            b.HasIndex(e => new { e.ProductId, e.Status });
            b.HasIndex(e => e.TenantId);
        });
    }
}
