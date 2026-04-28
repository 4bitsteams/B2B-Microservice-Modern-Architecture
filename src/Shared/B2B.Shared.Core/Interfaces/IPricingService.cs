namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// B2B pricing engine abstraction.
///
/// B2B pricing differs fundamentally from B2C: a single product can have
/// different prices for different customers, tiers, or order quantities.
/// Keeping pricing logic behind this interface means:
///   • Handlers never hard-code prices or discount percentages.
///   • The implementation can be swapped (flat rate → ERP-driven → AI-driven)
///     with zero application layer changes.
///   • Pricing rules are unit-testable in isolation.
/// </summary>
public interface IPricingService
{
    /// <summary>
    /// Returns the effective unit price for a product in the current tenant context,
    /// applying any volume discounts or customer-specific contract prices.
    /// </summary>
    Task<PricingResult> GetEffectivePriceAsync(
        PricingRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates pricing for multiple items in a single call — more efficient
    /// than looping over <see cref="GetEffectivePriceAsync"/> for each item.
    /// </summary>
    Task<IReadOnlyList<PricingResult>> GetEffectivePricesAsync(
        IReadOnlyList<PricingRequest> requests,
        CancellationToken ct = default);
}

public sealed record PricingRequest(
    Guid ProductId,
    Guid TenantId,
    Guid? CustomerId,
    decimal ListPrice,
    string Currency,
    int Quantity);

public sealed record PricingResult(
    Guid ProductId,
    decimal UnitPrice,
    decimal DiscountPercent,
    string Currency,
    string PricingTier);
