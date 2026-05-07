using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Commands.CreateDiscount;

/// <summary>
/// Command that creates a new promotional discount for the current tenant.
///
/// <para>
/// Discounts are applied automatically by the pricing engine — no code is
/// required from the buyer. Use <c>CreateCouponCommand</c> for code-based redemptions.
/// </para>
/// </summary>
/// <param name="Name">Required display name (max 200 chars).</param>
/// <param name="Type">Percentage or fixed-amount discount.</param>
/// <param name="Value">Discount magnitude — must be &gt; 0; ≤ 100 for percentage type.</param>
/// <param name="Description">Optional long-form description shown in the admin UI.</param>
/// <param name="StartDate">Optional UTC activation date; active immediately when omitted.</param>
/// <param name="EndDate">Optional UTC expiry date; never expires when omitted.</param>
/// <param name="MinimumOrderAmount">Optional minimum order subtotal required to qualify.</param>
/// <param name="MaxUsageCount">Optional redemption cap across all customers; unlimited when omitted.</param>
/// <param name="ApplicableProductId">Restrict to a specific product; applies to all products when omitted.</param>
/// <param name="ApplicableCategoryId">Restrict to a product category; applies to all categories when omitted.</param>
public sealed record CreateDiscountCommand(
    string Name,
    DiscountType Type,
    decimal Value,
    string? Description = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    decimal? MinimumOrderAmount = null,
    int? MaxUsageCount = null,
    Guid? ApplicableProductId = null,
    Guid? ApplicableCategoryId = null) : ICommand<CreateDiscountResponse>;

/// <summary>
/// Returned by <see cref="CreateDiscountHandler"/> on success.
/// </summary>
/// <param name="DiscountId">Persisted identifier of the new discount.</param>
/// <param name="Name">Display name of the created discount.</param>
/// <param name="Type">Type string ("Percentage" or "FixedAmount").</param>
/// <param name="Value">Discount value as stored.</param>
public sealed record CreateDiscountResponse(Guid DiscountId, string Name, string Type, decimal Value);
