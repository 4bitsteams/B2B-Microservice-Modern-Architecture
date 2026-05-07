using FluentValidation;

namespace B2B.Discount.Application.Commands.CreateDiscount;

/// <summary>
/// FluentValidation validator for <see cref="CreateDiscountCommand"/>.
/// Executed by <c>ValidationBehavior</c> in the MediatR pipeline before the handler runs.
///
/// <para>
/// Rules:
/// <list type="bullet">
///   <item><description>Name — required; max 200 chars.</description></item>
///   <item><description>Value — must be &gt; 0 (percentage max of 100 is enforced at the domain level in <c>Discount.Create</c>).</description></item>
///   <item><description>MaxUsageCount — when provided, must be &gt; 0.</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class CreateDiscountValidator : AbstractValidator<CreateDiscountCommand>
{
    public CreateDiscountValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.MaxUsageCount).GreaterThan(0).When(x => x.MaxUsageCount.HasValue);
    }
}
