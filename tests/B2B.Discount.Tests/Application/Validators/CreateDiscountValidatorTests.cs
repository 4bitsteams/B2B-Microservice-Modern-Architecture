using B2B.Discount.Application.Commands.CreateDiscount;
using B2B.Discount.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace B2B.Discount.Tests.Application.Validators;

public sealed class CreateDiscountValidatorTests
{
    private readonly CreateDiscountValidator _validator = new();

    private static CreateDiscountCommand Valid() =>
        new("Spring", DiscountType.Percentage, 10m);

    [Fact]
    public void Valid_ShouldPass() =>
        _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyName_ShouldFail(string name) =>
        _validator.Validate(Valid() with { Name = name }).IsValid.Should().BeFalse();

    [Fact]
    public void NameTooLong_ShouldFail() =>
        _validator.Validate(Valid() with { Name = new string('a', 201) }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveValue_ShouldFail(decimal value) =>
        _validator.Validate(Valid() with { Value = value }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void NonPositiveMaxUsage_ShouldFail(int max) =>
        _validator.Validate(Valid() with { MaxUsageCount = max }).IsValid.Should().BeFalse();

    [Fact]
    public void NullMaxUsage_ShouldPass() =>
        _validator.Validate(Valid() with { MaxUsageCount = null }).IsValid.Should().BeTrue();
}
