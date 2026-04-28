using FluentValidation;

namespace B2B.Order.Application.Commands.ShipOrder;

public sealed class ShipOrderValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.TrackingNumber).NotEmpty().MaximumLength(200);
    }
}
