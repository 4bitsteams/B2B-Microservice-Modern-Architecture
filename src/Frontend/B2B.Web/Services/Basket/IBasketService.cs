using B2B.Web.Models.Basket;

namespace B2B.Web.Services.Basket;

public interface IBasketService
{
    Task<BasketDto?> GetBasketAsync();
    Task<bool> AddItemAsync(AddToBasketRequest request);
    Task<bool> UpdateItemAsync(Guid productId, int quantity);
    Task<bool> RemoveItemAsync(Guid productId);
    Task<bool> ClearBasketAsync();
    Task<int> GetItemCountAsync();
}
