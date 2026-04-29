using Microsoft.EntityFrameworkCore;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Infrastructure.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PaymentEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            b.Property(e => e.Amount).HasPrecision(18, 2);
            b.HasIndex(e => e.OrderId);
            b.HasIndex(e => new { e.TenantId, e.CreatedAt });
        });

        modelBuilder.Entity<Invoice>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            b.HasIndex(e => e.InvoiceNumber).IsUnique();
            b.HasIndex(e => e.OrderId);
            b.HasIndex(e => new { e.TenantId, e.Status });
            b.Property(e => e.Subtotal).HasPrecision(18, 2);
            b.Property(e => e.TaxAmount).HasPrecision(18, 2);
            b.Property(e => e.TotalAmount).HasPrecision(18, 2);
            b.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            b.Property(e => e.Notes).HasMaxLength(1000);
        });
    }
}
