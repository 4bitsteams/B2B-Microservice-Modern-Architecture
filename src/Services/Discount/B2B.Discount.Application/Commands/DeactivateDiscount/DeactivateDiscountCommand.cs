using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Commands.DeactivateDiscount;

/// <summary>
/// Command that deactivates an active discount, preventing it from being
/// applied to new orders.
///
/// <para>
/// Returns <c>Error.Conflict</c> when the discount is already inactive.
/// Returns <c>Error.NotFound</c> when the discount does not exist or belongs
/// to a different tenant (security through obscurity).
/// </para>
///
/// <para>
/// Raises <c>DiscountDeactivatedEvent</c>, which can be consumed to purge
/// cached pricing rules or notify affected merchants.
/// </para>
/// </summary>
/// <param name="DiscountId">Identifier of the discount to deactivate.</param>
public sealed record DeactivateDiscountCommand(Guid DiscountId) : ICommand<DeactivateDiscountResponse>;

/// <summary>
/// Returned by <see cref="DeactivateDiscountHandler"/> on success.
/// </summary>
/// <param name="DiscountId">Identifier of the deactivated discount.</param>
/// <param name="IsActive">Always <see langword="false"/> on the success path.</param>
public sealed record DeactivateDiscountResponse(Guid DiscountId, bool IsActive);
