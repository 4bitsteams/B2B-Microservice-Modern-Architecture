using Microsoft.EntityFrameworkCore;
using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Infrastructure.Persistence;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Infrastructure.Persistence.Repositories;

public sealed class VendorRepository(VendorDbContext context)
    : BaseRepository<VendorEntity, Guid, VendorDbContext>(context), IVendorRepository
{
    public async Task<VendorEntity?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(v => v.ContactEmail == email.ToLowerInvariant() && v.TenantId == tenantId, ct);

    public async Task<VendorEntity?> GetByTaxIdAsync(string taxId, Guid tenantId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(v => v.TaxId == taxId && v.TenantId == tenantId, ct);
}
