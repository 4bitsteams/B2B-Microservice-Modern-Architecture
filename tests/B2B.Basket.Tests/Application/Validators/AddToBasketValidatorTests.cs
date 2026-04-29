using B2B.Basket.Application.Commands.AddToBasket;
using FluentAssertions;
using Xunit;

namespace B2B.Basket.Tests.Application.Validators;

public sealed class AddToBasketValidatorTests
{
    private readonly AddToBasketValidator _validator = new();

    private static AddToBasketCommand Valid() => new(
        ProductId: Guid.NewGuid(),
        ProductName: "Widget",
        Sku: "WGT-001",
        UnitPrice: 10m,
        Quantity: 1);

    [Fact]
    public void Valid_Command_ShouldPass()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyProductId_ShouldFail()
    {
        var cmd = Valid() with { ProductId = Guid.Empty };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AddToBasketCommand.ProductId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BlankProductName_ShouldFail(string name)
    {
        var cmd = Valid() with { ProductName = name };
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void TooLongProductName_ShouldFail()
    {
        var cmd = Valid() with { ProductName = new string('a', 301) };
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EmptySku_ShouldFail()
    {
        var cmd = Valid() with { Sku = "" };
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void NegativePrice_ShouldFail()
    {
        var cmd = Valid() with { UnitPrice = -0.01m };
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveQuantity_ShouldFail(int qty)
    {
        var cmd = Valid() with { Quantity = qty };
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }
}
