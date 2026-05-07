using FluentValidation;

namespace B2B.Order.Application.Commands.ShipOrder;

/// <summary>
/// FluentValidation validator for <see cref="ShipOrderCommand"/>.
/// Executed by <c>ValidationBehavior</c> in the MediatR pipeline before the handler runs.
///
/// <para>
/// Rules:
/// <list type="bullet">
///   <item><description>OrderId — must not be the empty GUID.</description></item>
///   <item><description>TrackingNumber — required; max 200 chars (carrier tracking codes vary widely in length).</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class ShipOrderValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.TrackingNumber).NotEmpty().MaximumLength(200);
    }
}
