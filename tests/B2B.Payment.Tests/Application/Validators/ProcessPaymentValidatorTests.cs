using B2B.Payment.Application.Commands.ProcessPayment;
using B2B.Payment.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace B2B.Payment.Tests.Application.Validators;

public sealed class ProcessPaymentValidatorTests
{
    private readonly ProcessPaymentValidator _validator = new();

    private static ProcessPaymentCommand Valid() =>
        new(Guid.NewGuid(), 99.99m, "USD", PaymentMethod.CreditCard);

    [Fact]
    public void Valid_ShouldPass() => _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyOrderId_ShouldFail() =>
        _validator.Validate(Valid() with { OrderId = Guid.Empty }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveAmount_ShouldFail(decimal amount) =>
        _validator.Validate(Valid() with { Amount = amount }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void BadCurrency_ShouldFail(string currency) =>
        _validator.Validate(Valid() with { Currency = currency }).IsValid.Should().BeFalse();
}
