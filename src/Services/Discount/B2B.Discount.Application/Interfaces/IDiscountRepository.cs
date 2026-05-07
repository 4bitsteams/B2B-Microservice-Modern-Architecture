using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using CouponEntity = B2B.Discount.Domain.Entities.Coupon;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Application.Interfaces;

/// <summary>
/// Write repository for <see cref="DiscountEntity"/> aggregates.
/// Targets the primary database with change tracking enabled.
/// Inject into command handlers only — never into query handlers.
/// </summary>
public interface IDiscountRepository : IRepository<DiscountEntity, Guid>
{
    /// <summary>
    /// Returns all active discounts for the given tenant, ordered by creation date descending.
    /// Used by the pricing engine to evaluate applicable promotions at checkout.
    /// </summary>
    /// <param name="tenantId">The tenant whose active discounts to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<DiscountEntity>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// Read-only repository for <see cref="DiscountEntity"/> aggregates.
/// Targets the read replica with <c>QueryTrackingBehavior.NoTracking</c>.
/// Inject into query handlers only — <c>SaveChangesAsync</c> is not exposed.
/// </summary>
public interface IReadDiscountRepository : IReadRepository<DiscountEntity, Guid>
{
    /// <summary>
    /// Returns a paged list of discounts for the given tenant, ordered by creation date descending.
    /// </summary>
    /// <param name="tenantId">The tenant whose discounts to retrieve.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Maximum items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PagedList<DiscountEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>
/// Write repository for <see cref="CouponEntity"/> aggregates.
/// Targets the primary database with change tracking enabled.
/// Inject into command handlers only — never into query handlers.
/// </summary>
public interface ICouponRepository : IRepository<CouponEntity, Guid>
{
    /// <summary>
    /// Looks up a coupon by its redemption code within the given tenant.
    /// The lookup is case-insensitive because codes are always stored upper-cased.
    /// </summary>
    /// <param name="code">The coupon code to look up (case-insensitive).</param>
    /// <param name="tenantId">The tenant scope — coupons are not shared across tenants.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <c>Coupon</c>, or <see langword="null"/> if not found.</returns>
    Task<CouponEntity?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// Read-only repository for <see cref="CouponEntity"/> aggregates.
/// Targets the read replica with <c>QueryTrackingBehavior.NoTracking</c>.
/// Inject into query handlers only — <c>SaveChangesAsync</c> is not exposed.
/// </summary>
public interface IReadCouponRepository : IReadRepository<CouponEntity, Guid>
{
    /// <summary>
    /// Returns a paged list of coupons for the given tenant, ordered by creation date descending.
    /// </summary>
    /// <param name="tenantId">The tenant whose coupons to retrieve.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Maximum items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PagedList<CouponEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}
