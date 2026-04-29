using System.Text.Json;
using B2B.Basket.Application.Interfaces;
using StackExchange.Redis;
using BasketEntity = B2B.Basket.Domain.Entities.Basket;

namespace B2B.Basket.Infrastructure.Persistence;

/// <summary>
/// Redis-backed basket repository. Baskets are stored as JSON strings keyed by
/// "basket:{tenantId}:{customerId}" with a 7-day sliding expiry.
/// </summary>
public sealed class RedisBasketRepository(IConnectionMultiplexer redis) : IBasketRepository
{
    private static readonly TimeSpan BasketExpiry = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static string GetKey(Guid customerId, Guid tenantId) =>
        $"basket:{tenantId}:{customerId}";

    public async Task<BasketEntity?> GetAsync(Guid customerId, Guid tenantId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = GetKey(customerId, tenantId);
        var value = await db.StringGetAsync(key);

        if (value.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<BasketRedisDto>((string)value!, JsonOptions)?.ToBasket();
    }

    public async Task<BasketEntity> GetOrCreateAsync(Guid customerId, Guid tenantId, CancellationToken ct = default)
    {
        var existing = await GetAsync(customerId, tenantId, ct);
        if (existing is not null) return existing;

        var newBasket = BasketEntity.CreateFor(customerId, tenantId);
        await SaveAsync(newBasket, ct);
        return newBasket;
    }

    public async Task SaveAsync(BasketEntity basket, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = GetKey(basket.CustomerId, basket.TenantId);
        var dto = BasketRedisDto.FromBasket(basket);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        await db.StringSetAsync(key, json, BasketExpiry);
    }

    public async Task DeleteAsync(Guid customerId, Guid tenantId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(GetKey(customerId, tenantId));
    }

    // ── Internal DTO (avoids exposing private setters to the JSON serializer) ──

    private sealed class BasketRedisDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid TenantId { get; set; }
        public DateTime LastModified { get; set; }
        public List<BasketItemRedisDto> Items { get; set; } = [];

        public static BasketRedisDto FromBasket(BasketEntity basket) => new()
        {
            Id = basket.Id,
            CustomerId = basket.CustomerId,
            TenantId = basket.TenantId,
            LastModified = basket.LastModified,
            Items = basket.Items.Select(i => new BasketItemRedisDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Sku = i.Sku,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                ImageUrl = i.ImageUrl
            }).ToList()
        };

        public BasketEntity ToBasket()
        {
            // Reconstruct via reflection-free approach using the public factory
            var basket = BasketEntity.CreateFor(CustomerId, TenantId);
            foreach (var item in Items)
                basket.AddItem(item.ProductId, item.ProductName, item.Sku, item.UnitPrice, item.Quantity, item.ImageUrl);
            basket.ClearDomainEvents(); // reconstruction doesn't emit events
            return basket;
        }
    }

    private sealed class BasketItemRedisDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public string? ImageUrl { get; set; }
    }
}
