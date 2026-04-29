using Microsoft.EntityFrameworkCore;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Payment.Infrastructure.Persistence.Repositories;

public sealed class InvoiceReadRepository(IDbContextFactory<PaymentDbContext> factory)
    : BaseReadRepository<Invoice, Guid, PaymentDbContext>(factory), IReadInvoiceRepository
{
    public async Task<PagedList<Invoice>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var query = ctx.Invoices.Where(i => i.TenantId == tenantId).OrderByDescending(i => i.IssuedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedList<Invoice>.Create(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<Invoice>> GetOverdueAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        return await ctx.Invoices
            .Where(i => i.TenantId == tenantId && i.Status == InvoiceStatus.Issued && i.DueAt < now)
            .ToListAsync(ct);
    }
}
