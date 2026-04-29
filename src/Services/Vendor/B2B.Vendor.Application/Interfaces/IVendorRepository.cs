using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Application.Interfaces;

public interface IVendorRepository : IRepository<VendorEntity, Guid>
{
    Task<VendorEntity?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<VendorEntity?> GetByTaxIdAsync(string taxId, Guid tenantId, CancellationToken ct = default);
}

public interface IReadVendorRepository : IReadRepository<VendorEntity, Guid>
{
    Task<PagedList<VendorEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedList<VendorEntity>> GetByStatusAsync(Guid tenantId, B2B.Vendor.Domain.Entities.VendorStatus status, int page, int pageSize, CancellationToken ct = default);
}
