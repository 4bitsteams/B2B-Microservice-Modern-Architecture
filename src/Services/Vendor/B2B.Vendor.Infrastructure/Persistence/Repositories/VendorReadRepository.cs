using Microsoft.EntityFrameworkCore;
using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;
using VendorStatus = B2B.Vendor.Domain.Entities.VendorStatus;

namespace B2B.Vendor.Infrastructure.Persistence.Repositories;

public sealed class VendorReadRepository(IDbContextFactory<VendorDbContext> factory)
    : BaseReadRepository<VendorEntity, Guid, VendorDbContext>(factory), IReadVendorRepository
{
    public async Task<PagedList<VendorEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var query = ctx.Vendors.Where(v => v.TenantId == tenantId).OrderByDescending(v => v.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedList<VendorEntity>.Create(items, page, pageSize, total);
    }

    public async Task<PagedList<VendorEntity>> GetByStatusAsync(Guid tenantId, VendorStatus status, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var query = ctx.Vendors.Where(v => v.TenantId == tenantId && v.Status == status).OrderByDescending(v => v.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedList<VendorEntity>.Create(items, page, pageSize, total);
    }
}
