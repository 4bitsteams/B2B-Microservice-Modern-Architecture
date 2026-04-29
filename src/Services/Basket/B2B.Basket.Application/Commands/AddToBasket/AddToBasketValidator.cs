using FluentValidation;

namespace B2B.Basket.Application.Commands.AddToBasket;

public sealed class AddToBasketValidator : AbstractValidator<AddToBasketCommand>
{
    public AddToBasketValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
