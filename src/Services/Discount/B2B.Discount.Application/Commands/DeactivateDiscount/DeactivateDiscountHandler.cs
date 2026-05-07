using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Application.Commands.DeactivateDiscount;

/// <summary>
/// Handles <see cref="DeactivateDiscountCommand"/> by transitioning an active
/// discount to the inactive state.
///
/// <para>
/// Guard conditions (both return without modifying state):
/// <list type="bullet">
///   <item>
///     <description>
///       The discount does not exist or belongs to a different tenant →
///       <c>Error.NotFound</c> (cross-tenant existence is not revealed).
///     </description>
///   </item>
///   <item>
///     <description>
///       The discount is already inactive → <c>Error.Conflict</c>
///       (idempotency: deactivating twice is a no-op error, not a silent success).
///     </description>
///   </item>
/// </list>
/// </para>
/// </summary>
public sealed class DeactivateDiscountHandler(
    IDiscountRepository discountRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateDiscountCommand, DeactivateDiscountResponse>
{
    /// <inheritdoc/>
    public async Task<Result<DeactivateDiscountResponse>> Handle(
        DeactivateDiscountCommand request, CancellationToken cancellationToken)
    {
        var discount = await discountRepository.GetByIdAsync(request.DiscountId, cancellationToken);

        if (discount is null || discount.TenantId != currentUser.TenantId)
            return Error.NotFound("Discount.NotFound", $"Discount {request.DiscountId} not found.");

        if (!discount.IsActive)
            return Error.Conflict("Discount.AlreadyInactive", "Discount is already inactive.");

        discount.Deactivate();
        discountRepository.Update(discount);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeactivateDiscountResponse(discount.Id, discount.IsActive);
    }
}
