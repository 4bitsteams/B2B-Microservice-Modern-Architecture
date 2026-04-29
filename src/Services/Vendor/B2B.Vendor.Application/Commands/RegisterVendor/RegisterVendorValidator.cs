using FluentValidation;

namespace B2B.Vendor.Application.Commands.RegisterVendor;

public sealed class RegisterVendorValidator : AbstractValidator<RegisterVendorCommand>
{
    public RegisterVendorValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Country).NotEmpty().Length(2, 3);
    }
}
