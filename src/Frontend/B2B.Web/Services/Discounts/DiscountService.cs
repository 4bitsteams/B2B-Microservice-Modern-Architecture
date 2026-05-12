using System.Net.Http.Json;
using B2B.Web.Models.Common;
using B2B.Web.Models.Discounts;

namespace B2B.Web.Services.Discounts;

public sealed class DiscountService(IHttpClientFactory httpClientFactory) : IDiscountService
{
    private HttpClient Http => httpClientFactory.CreateClient("auth");

    public Task<PagedResult<DiscountDto>?> GetDiscountsAsync(int page = 1, int pageSize = 20)
        => Http.GetFromJsonAsync<PagedResult<DiscountDto>>(
            $"/api/discounts?page={page}&pageSize={pageSize}");

    public Task<DiscountDto?> GetDiscountAsync(Guid id)
        => Http.GetFromJsonAsync<DiscountDto>($"/api/discounts/{id}");

    public async Task<DiscountDto?> CreateDiscountAsync(CreateDiscountRequest request)
    {
        var response = await Http.PostAsJsonAsync("/api/discounts", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DiscountDto>()
            : null;
    }

    public async Task<bool> DeactivateDiscountAsync(Guid id)
    {
        var response = await Http.PostAsync($"/api/discounts/{id}/deactivate", null);
        return response.IsSuccessStatusCode;
    }

    public Task<PagedResult<CouponDto>?> GetCouponsAsync(int page = 1, int pageSize = 20)
        => Http.GetFromJsonAsync<PagedResult<CouponDto>>(
            $"/api/coupons?page={page}&pageSize={pageSize}");

    public async Task<CouponDto?> CreateCouponAsync(CreateCouponRequest request)
    {
        var response = await Http.PostAsJsonAsync("/api/coupons", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CouponDto>()
            : null;
    }

    public async Task<ApplyCouponResponse?> ApplyCouponAsync(ApplyCouponRequest request)
    {
        var response = await Http.PostAsJsonAsync("/api/coupons/apply", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ApplyCouponResponse>()
            : null;
    }
}
