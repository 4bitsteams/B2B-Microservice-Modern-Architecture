using FluentAssertions;
using Xunit;
using B2B.Product.Domain.ValueObjects;

namespace B2B.Product.Tests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void Create_WithValidAmount_ShouldSucceed()
    {
        var money = Money.Of(99.99m, "USD");
        money.Amount.Should().Be(99.99m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        var act = () => Money.Of(-1m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_WithSameCurrency_ShouldReturn_CorrectSum()
    {
        var a = Money.Of(10m, "USD");
        var b = Money.Of(5m, "USD");
        var result = a.Add(b);
        result.Amount.Should().Be(15m);
    }

    [Fact]
    public void Add_WithDifferentCurrencies_ShouldThrow()
    {
        var usd = Money.Of(10m, "USD");
        var eur = Money.Of(10m, "EUR");
        var act = () => usd.Add(eur);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        var a = Money.Of(50m, "USD");
        var b = Money.Of(50m, "USD");
        a.Should().Be(b);
    }

    [Fact]
    public void Multiply_ShouldReturnCorrectValue()
    {
        var price = Money.Of(25m, "USD");
        var total = price.Multiply(4);
        total.Amount.Should().Be(100m);
    }
}
