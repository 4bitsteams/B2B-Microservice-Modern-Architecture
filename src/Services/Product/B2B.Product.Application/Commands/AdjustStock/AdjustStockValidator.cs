using FluentValidation;

namespace B2B.Product.Application.Commands.AdjustStock;

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).NotEqual(0).WithMessage("Quantity adjustment must not be zero.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
