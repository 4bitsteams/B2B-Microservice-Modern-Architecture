using B2B.Discount.Application.Commands.CreateCoupon;
using B2B.Discount.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace B2B.Discount.Tests.Application.Validators;

public sealed class CreateCouponValidatorTests
{
    private readonly CreateCouponValidator _validator = new();

    private static CreateCouponCommand Valid() =>
        new("SAVE10", "Save 10", DiscountType.Percentage, 10m, MaxUsageCount: 5);

    [Fact]
    public void Valid_ShouldPass() => _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyCode_ShouldFail() =>
        _validator.Validate(Valid() with { Code = "" }).IsValid.Should().BeFalse();

    [Fact]
    public void CodeTooLong_ShouldFail() =>
        _validator.Validate(Valid() with { Code = new string('a', 51) }).IsValid.Should().BeFalse();

    [Fact]
    public void EmptyName_ShouldFail() =>
        _validator.Validate(Valid() with { Name = "" }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveValue_ShouldFail(decimal v) =>
        _validator.Validate(Valid() with { Value = v }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveMaxUsage_ShouldFail(int m) =>
        _validator.Validate(Valid() with { MaxUsageCount = m }).IsValid.Should().BeFalse();
}
