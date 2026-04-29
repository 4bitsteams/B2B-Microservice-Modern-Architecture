using FluentValidation;

namespace B2B.Vendor.Application.Commands.UpdateVendorProfile;

public sealed class UpdateVendorProfileValidator : AbstractValidator<UpdateVendorProfileCommand>
{
    public UpdateVendorProfileValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Country).NotEmpty().Length(2, 100);
    }
}
