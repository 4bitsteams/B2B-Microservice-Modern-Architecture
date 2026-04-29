using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Commands.DeactivateDiscount;

public sealed record DeactivateDiscountCommand(Guid DiscountId) : ICommand<DeactivateDiscountResponse>;

public sealed record DeactivateDiscountResponse(Guid DiscountId, bool IsActive);
