using FluentValidation;

namespace B2B.Shipping.Application.Commands.UpdateTrackingInfo;

public sealed class UpdateTrackingInfoValidator : AbstractValidator<UpdateTrackingInfoCommand>
{
    public UpdateTrackingInfoValidator()
    {
        RuleFor(x => x.NewTrackingNumber).NotEmpty().MaximumLength(100);
    }
}
