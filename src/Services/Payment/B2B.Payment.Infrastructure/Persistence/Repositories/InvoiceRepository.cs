using Microsoft.EntityFrameworkCore;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Payment.Infrastructure.Persistence.Repositories;

public sealed class InvoiceRepository(PaymentDbContext context)
    : BaseRepository<Invoice, Guid, PaymentDbContext>(context), IInvoiceRepository
{
    public async Task<Invoice?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(i => i.OrderId == orderId, ct);

    public async Task<string> GenerateInvoiceNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await Context.Invoices.CountAsync(i => i.CreatedAt.Year == year, ct);
        return $"INV-{year}-{(count + 1):D6}";
    }
}
