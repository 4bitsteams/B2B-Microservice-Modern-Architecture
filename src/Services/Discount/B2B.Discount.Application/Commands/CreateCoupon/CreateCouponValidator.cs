using FluentValidation;

namespace B2B.Discount.Application.Commands.CreateCoupon;

/// <summary>
/// FluentValidation validator for <see cref="CreateCouponCommand"/>.
/// Executed by <c>ValidationBehavior</c> in the MediatR pipeline before the handler runs.
///
/// <para>
/// Rules:
/// <list type="bullet">
///   <item><description>Code — required; max 50 chars (stored upper-cased in the domain).</description></item>
///   <item><description>Name — required; max 200 chars.</description></item>
///   <item><description>Value — must be &gt; 0.</description></item>
///   <item><description>MaxUsageCount — must be &gt; 0 (defaults to 1 in the command).</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class CreateCouponValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.MaxUsageCount).GreaterThan(0);
    }
}
