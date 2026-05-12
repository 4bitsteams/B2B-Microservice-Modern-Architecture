using System.Net.Http.Json;
using B2B.Web.Models.Basket;

namespace B2B.Web.Services.Basket;

public sealed class BasketService(IHttpClientFactory httpClientFactory) : IBasketService
{
    private HttpClient Http => httpClientFactory.CreateClient("auth");

    public Task<BasketDto?> GetBasketAsync()
        => Http.GetFromJsonAsync<BasketDto>("/api/basket");

    public async Task<bool> AddItemAsync(AddToBasketRequest request)
    {
        var response = await Http.PostAsJsonAsync("/api/basket/items", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateItemAsync(Guid productId, int quantity)
    {
        var response = await Http.PutAsJsonAsync(
            $"/api/basket/items/{productId}",
            new UpdateBasketItemRequest(quantity));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveItemAsync(Guid productId)
    {
        var response = await Http.DeleteAsync($"/api/basket/items/{productId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ClearBasketAsync()
    {
        var response = await Http.DeleteAsync("/api/basket");
        return response.IsSuccessStatusCode;
    }

    public async Task<int> GetItemCountAsync()
    {
        var basket = await GetBasketAsync();
        return basket?.TotalItems ?? 0;
    }
}
