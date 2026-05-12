using System.Net.Http.Json;
using B2B.Web.Models.Common;
using B2B.Web.Models.Orders;

namespace B2B.Web.Services.Orders;

public sealed class OrderService(IHttpClientFactory httpClientFactory) : IOrderService
{
    private HttpClient Http => httpClientFactory.CreateClient("auth");

    public async Task<PagedResult<OrderSummaryDto>?> GetOrdersAsync(
        int page = 1, int pageSize = 20, string? status = null)
    {
        var query = $"/api/orders?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status)) query += $"&status={status}";
        return await Http.GetFromJsonAsync<PagedResult<OrderSummaryDto>>(query);
    }

    public Task<OrderDto?> GetOrderAsync(Guid id)
        => Http.GetFromJsonAsync<OrderDto>($"/api/orders/{id}");

    public async Task<OrderDto?> CreateOrderAsync(CreateOrderRequest request)
    {
        var response = await Http.PostAsJsonAsync("/api/orders", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<OrderDto>()
            : null;
    }

    public async Task<bool> ConfirmOrderAsync(Guid id)
    {
        var response = await Http.PostAsync($"/api/orders/{id}/confirm", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CancelOrderAsync(Guid id, string reason)
    {
        var response = await Http.PostAsJsonAsync($"/api/orders/{id}/cancel",
            new CancelOrderRequest(reason));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ShipOrderAsync(Guid id, ShipOrderRequest request)
    {
        var response = await Http.PostAsJsonAsync($"/api/orders/{id}/ship", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeliverOrderAsync(Guid id)
    {
        var response = await Http.PostAsync($"/api/orders/{id}/deliver", null);
        return response.IsSuccessStatusCode;
    }
}
