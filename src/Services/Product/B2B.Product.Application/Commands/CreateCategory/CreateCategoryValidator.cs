using FluentValidation;

namespace B2B.Product.Application.Commands.CreateCategory;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug may only contain lowercase letters, digits, and hyphens.");
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
