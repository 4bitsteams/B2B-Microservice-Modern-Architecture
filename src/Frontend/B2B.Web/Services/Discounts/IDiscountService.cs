using B2B.Web.Models.Common;
using B2B.Web.Models.Discounts;

namespace B2B.Web.Services.Discounts;

public interface IDiscountService
{
    Task<PagedResult<DiscountDto>?> GetDiscountsAsync(int page = 1, int pageSize = 20);
    Task<DiscountDto?> GetDiscountAsync(Guid id);
    Task<DiscountDto?> CreateDiscountAsync(CreateDiscountRequest request);
    Task<bool> DeactivateDiscountAsync(Guid id);
    Task<PagedResult<CouponDto>?> GetCouponsAsync(int page = 1, int pageSize = 20);
    Task<CouponDto?> CreateCouponAsync(CreateCouponRequest request);
    Task<ApplyCouponResponse?> ApplyCouponAsync(ApplyCouponRequest request);
}
