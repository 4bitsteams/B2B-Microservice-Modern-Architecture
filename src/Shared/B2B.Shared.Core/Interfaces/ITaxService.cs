namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Pluggable tax calculation.
///
/// In B2B scenarios tax rules are jurisdiction-specific and can be overridden
/// per customer or product category. Injecting ITaxService into the Order
/// aggregate (via a domain service method) keeps the rate out of the entity
/// and allows swapping rules without touching domain code.
/// </summary>
public interface ITaxService
{
    /// <summary>
    /// Returns the tax rate (0.0 – 1.0) that applies to the given subtotal
    /// for the specified tenant and optional category context.
    /// </summary>
    Task<decimal> GetTaxRateAsync(
        Guid tenantId,
        string? productCategory = null,
        CancellationToken ct = default);

    /// <summary>Calculates the tax amount for the given subtotal.</summary>
    Task<decimal> CalculateTaxAsync(
        decimal subtotal,
        Guid tenantId,
        string? productCategory = null,
        CancellationToken ct = default);
}
