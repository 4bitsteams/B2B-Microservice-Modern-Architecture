using B2B.Vendor.Application.Commands.RegisterVendor;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace B2B.Vendor.Tests.Application.Validators;

public sealed class RegisterVendorValidatorTests
{
    private readonly RegisterVendorValidator _validator = new();

    private static RegisterVendorCommand Valid() => new(
        CompanyName: "Acme Corp",
        ContactEmail: "contact@acme.com",
        TaxId: "TX-12345",
        Address: "1 Main St",
        City: "New York",
        Country: "US");

    [Fact]
    public void Validate_Valid_ShouldPass()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyCompanyName_ShouldFail(string name)
    {
        var result = _validator.TestValidate(Valid() with { CompanyName = name });
        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Fact]
    public void Validate_CompanyNameTooLong_ShouldFail()
    {
        var result = _validator.TestValidate(Valid() with { CompanyName = new string('A', 301) });
        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_ShouldFail(string email)
    {
        var result = _validator.TestValidate(Valid() with { ContactEmail = email });
        result.ShouldHaveValidationErrorFor(x => x.ContactEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyTaxId_ShouldFail(string taxId)
    {
        var result = _validator.TestValidate(Valid() with { TaxId = taxId });
        result.ShouldHaveValidationErrorFor(x => x.TaxId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyAddress_ShouldFail(string address)
    {
        var result = _validator.TestValidate(Valid() with { Address = address });
        result.ShouldHaveValidationErrorFor(x => x.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyCity_ShouldFail(string city)
    {
        var result = _validator.TestValidate(Valid() with { City = city });
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]          // too short
    [InlineData("USAX")]       // too long (>3)
    public void Validate_InvalidCountryCode_ShouldFail(string country)
    {
        var result = _validator.TestValidate(Valid() with { Country = country });
        result.ShouldHaveValidationErrorFor(x => x.Country);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USA")]
    public void Validate_ValidCountryCode_ShouldPass(string country)
    {
        var result = _validator.TestValidate(Valid() with { Country = country });
        result.ShouldNotHaveValidationErrorFor(x => x.Country);
    }
}
