using FluentAssertions;
using Xunit;
using B2B.Identity.Application.Commands.RegisterUser;

namespace B2B.Identity.Tests.Application.Validators;

public sealed class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _validator = new();

    private static RegisterUserCommand Valid() =>
        new("Jane", "Doe", "jane@acme.com", "P@ssw0rd!", "acme");

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        var result = _validator.Validate(Valid());
        result.IsValid.Should().BeTrue();
    }

    // ──────────────────────────────────────────────
    // First name
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyFirstName_ShouldFail(string firstName)
    {
        var result = _validator.Validate(Valid() with { FirstName = firstName });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.FirstName));
    }

    [Fact]
    public void Validate_WithFirstNameExceedingMaxLength_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { FirstName = new string('A', 101) });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.FirstName));
    }

    // ──────────────────────────────────────────────
    // Last name
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyLastName_ShouldFail(string lastName)
    {
        var result = _validator.Validate(Valid() with { LastName = lastName });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.LastName));
    }

    [Fact]
    public void Validate_WithLastNameExceedingMaxLength_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { LastName = new string('B', 101) });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.LastName));
    }

    // ──────────────────────────────────────────────
    // Email
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyEmail_ShouldFail(string email)
    {
        var result = _validator.Validate(Valid() with { Email = email });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.Email));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign")]
    public void Validate_WithInvalidEmailFormat_ShouldFail(string email)
    {
        var result = _validator.Validate(Valid() with { Email = email });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.Email));
    }

    [Fact]
    public void Validate_WithEmailExceedingMaxLength_ShouldFail()
    {
        var email = new string('a', 251) + "@x.com";
        var result = _validator.Validate(Valid() with { Email = email });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.Email));
    }

    // ──────────────────────────────────────────────
    // Password strength rules
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_WithPasswordTooShort_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { Password = "P@ss1" }); // 5 chars
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.Password));
    }

    [Fact]
    public void Validate_WithPasswordMissingUppercase_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { Password = "p@ssw0rd!" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterUserCommand.Password) &&
            e.ErrorMessage.Contains("uppercase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WithPasswordMissingLowercase_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { Password = "P@SSW0RD!" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterUserCommand.Password) &&
            e.ErrorMessage.Contains("lowercase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WithPasswordMissingDigit_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { Password = "P@ssword!" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterUserCommand.Password) &&
            e.ErrorMessage.Contains("digit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WithPasswordMissingSpecialCharacter_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { Password = "Passw0rd" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterUserCommand.Password) &&
            e.ErrorMessage.Contains("special", StringComparison.OrdinalIgnoreCase));
    }

    // ──────────────────────────────────────────────
    // Tenant slug
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyTenantSlug_ShouldFail(string slug)
    {
        var result = _validator.Validate(Valid() with { TenantSlug = slug });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.TenantSlug));
    }

    [Fact]
    public void Validate_WithTenantSlugExceedingMaxLength_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { TenantSlug = new string('x', 101) });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.TenantSlug));
    }
}
