using BasketEntity = B2B.Basket.Domain.Entities.Basket;

namespace B2B.Basket.Application.Interfaces;

/// <summary>
/// Redis-backed basket repository. Gets or creates the basket for a customer/tenant,
/// persists mutations, and removes it on checkout.
/// </summary>
public interface IBasketRepository
{
    Task<BasketEntity?> GetAsync(Guid customerId, Guid tenantId, CancellationToken ct = default);
    Task<BasketEntity> GetOrCreateAsync(Guid customerId, Guid tenantId, CancellationToken ct = default);
    Task SaveAsync(BasketEntity basket, CancellationToken ct = default);
    Task DeleteAsync(Guid customerId, Guid tenantId, CancellationToken ct = default);
}
