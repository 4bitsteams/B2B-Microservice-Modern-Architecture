using FluentValidation;

namespace B2B.Basket.Application.Commands.UpdateBasketItem;

public sealed class UpdateBasketItemValidator : AbstractValidator<UpdateBasketItemCommand>
{
    public UpdateBasketItemValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
