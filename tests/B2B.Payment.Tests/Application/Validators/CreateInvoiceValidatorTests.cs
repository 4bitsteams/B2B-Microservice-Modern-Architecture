using B2B.Payment.Application.Commands.CreateInvoice;
using FluentAssertions;
using Xunit;

namespace B2B.Payment.Tests.Application.Validators;

public sealed class CreateInvoiceValidatorTests
{
    private readonly CreateInvoiceValidator _validator = new();

    private static CreateInvoiceCommand Valid() =>
        new(Guid.NewGuid(), 100m, 7m, "USD", 30);

    [Fact]
    public void Valid_ShouldPass() => _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyOrderId_ShouldFail() =>
        _validator.Validate(Valid() with { OrderId = Guid.Empty }).IsValid.Should().BeFalse();

    [Fact]
    public void NegativeSubtotal_ShouldFail() =>
        _validator.Validate(Valid() with { Subtotal = -1m }).IsValid.Should().BeFalse();

    [Fact]
    public void NegativeTax_ShouldFail() =>
        _validator.Validate(Valid() with { TaxAmount = -1m }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("USDD")]
    public void BadCurrency_ShouldFail(string currency) =>
        _validator.Validate(Valid() with { Currency = currency }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public void OutOfRangeNetTerms_ShouldFail(int days) =>
        _validator.Validate(Valid() with { NetTermsDays = days }).IsValid.Should().BeFalse();
}
