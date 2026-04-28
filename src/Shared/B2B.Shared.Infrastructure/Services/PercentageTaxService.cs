using Microsoft.Extensions.Configuration;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Services;

/// <summary>
/// Default tax service — applies a single configurable flat rate.
///
/// Configure in appsettings:
/// <code>
/// "TaxSettings": { "DefaultRate": 0.10 }
/// </code>
///
/// For jurisdiction-aware tax (US sales tax, EU VAT), swap this implementation
/// with one backed by a tax API (Avalara, TaxJar) — zero changes in domain or
/// application layers because both depend only on <see cref="ITaxService"/>.
/// </summary>
public sealed class PercentageTaxService(IConfiguration configuration) : ITaxService
{
    private readonly decimal _defaultRate =
        configuration.GetValue("TaxSettings:DefaultRate", 0.10m);

    public Task<decimal> GetTaxRateAsync(
        Guid tenantId,
        string? productCategory = null,
        CancellationToken ct = default) =>
        Task.FromResult(_defaultRate);

    public Task<decimal> CalculateTaxAsync(
        decimal subtotal,
        Guid tenantId,
        string? productCategory = null,
        CancellationToken ct = default) =>
        Task.FromResult(Math.Round(subtotal * _defaultRate, 2));
}
