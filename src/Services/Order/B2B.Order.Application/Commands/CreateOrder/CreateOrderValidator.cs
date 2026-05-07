using FluentValidation;

namespace B2B.Order.Application.Commands.CreateOrder;

/// <summary>
/// FluentValidation validator for <see cref="CreateOrderCommand"/>.
/// Executed by <c>ValidationBehavior</c> in the MediatR pipeline before the handler runs.
///
/// <para>
/// Rules:
/// <list type="bullet">
///   <item><description>ShippingAddress — required; street max 300 chars; city max 100 chars; country 2–3 chars (ISO 3166-1).</description></item>
///   <item><description>Items — at least one item must be provided.</description></item>
///   <item><description>Each item — ProductId not empty; Quantity &gt; 0; UnitPrice ≥ 0; ProductName not empty (max 300 chars).</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.ShippingAddress).NotNull();
        RuleFor(x => x.ShippingAddress.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ShippingAddress.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShippingAddress.Country).NotEmpty().Length(2, 3);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Order must have at least one item.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.ProductName).NotEmpty().MaximumLength(300);
        });
    }
}
