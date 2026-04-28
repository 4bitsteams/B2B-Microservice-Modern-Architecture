using Microsoft.Extensions.Configuration;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Services;

/// <summary>
/// Volume-tiered pricing service.
///
/// Applies quantity-based discount brackets configured in appsettings.
/// Replace with an ERP/CPQ-backed implementation for full B2B contract pricing.
///
/// Configuration example:
/// <code>
/// "PricingSettings": {
///   "Tiers": [
///     { "MinQuantity": 1,   "DiscountPercent": 0.00 },
///     { "MinQuantity": 10,  "DiscountPercent": 0.05 },
///     { "MinQuantity": 50,  "DiscountPercent": 0.10 },
///     { "MinQuantity": 100, "DiscountPercent": 0.15 }
///   ]
/// }
/// </code>
///
/// If no configuration is present, the list price is returned unchanged (0% discount).
/// </summary>
public sealed class TieredPricingService(IConfiguration configuration) : IPricingService
{
    private readonly IReadOnlyList<PricingTierConfig> _tiers = LoadTiers(configuration);

    public Task<PricingResult> GetEffectivePriceAsync(
        PricingRequest request, CancellationToken ct = default)
        => Task.FromResult(Calculate(request));

    public Task<IReadOnlyList<PricingResult>> GetEffectivePricesAsync(
        IReadOnlyList<PricingRequest> requests, CancellationToken ct = default)
    {
        IReadOnlyList<PricingResult> results = requests.Select(Calculate).ToList();
        return Task.FromResult(results);
    }

    private PricingResult Calculate(PricingRequest request)
    {
        // Find the highest tier whose MinQuantity does not exceed the requested quantity.
        var tier = _tiers
            .Where(t => request.Quantity >= t.MinQuantity)
            .MaxBy(t => t.MinQuantity)
            ?? new PricingTierConfig(1, 0m);

        var discountedPrice = Math.Round(request.ListPrice * (1 - tier.DiscountPercent), 2);

        return new PricingResult(
            request.ProductId,
            discountedPrice,
            tier.DiscountPercent,
            request.Currency,
            $"Tier-{tier.MinQuantity}+");
    }

    private static IReadOnlyList<PricingTierConfig> LoadTiers(IConfiguration configuration)
    {
        var tiers = configuration
            .GetSection("PricingSettings:Tiers")
            .Get<List<PricingTierConfig>>();

        if (tiers is null || tiers.Count == 0)
            return [new PricingTierConfig(1, 0m)];

        return tiers.OrderBy(t => t.MinQuantity).ToList();
    }

    private sealed record PricingTierConfig(int MinQuantity, decimal DiscountPercent);
}
