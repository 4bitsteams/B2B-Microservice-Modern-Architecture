using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Application.Interfaces;

public interface IDiscountRepository : IRepository<DiscountEntity, Guid>
{
    Task<IReadOnlyList<DiscountEntity>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public interface IReadDiscountRepository : IReadRepository<DiscountEntity, Guid>
{
    Task<PagedList<DiscountEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}

public interface ICouponRepository : IRepository<B2B.Discount.Domain.Entities.Coupon, Guid>
{
    Task<B2B.Discount.Domain.Entities.Coupon?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default);
}

public interface IReadCouponRepository : IReadRepository<B2B.Discount.Domain.Entities.Coupon, Guid>
{
    Task<PagedList<B2B.Discount.Domain.Entities.Coupon>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}
