using Microsoft.EntityFrameworkCore;
using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Infrastructure.Persistence.Repositories;

public sealed class PaymentReadRepository(IDbContextFactory<PaymentDbContext> factory)
    : BaseReadRepository<PaymentEntity, Guid, PaymentDbContext>(factory), IReadPaymentRepository
{
    public async Task<PagedList<PaymentEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var query = ctx.Payments.Where(p => p.TenantId == tenantId).OrderByDescending(p => p.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedList<PaymentEntity>.Create(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<PaymentEntity>> GetByCustomerAsync(Guid customerId, Guid tenantId, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Payments.Where(p => p.CustomerId == customerId && p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
    }
}
