using FluentAssertions;
using Xunit;
using B2B.Basket.Domain.Entities;

namespace B2B.Basket.Tests.Domain;

public sealed class BasketItemTests
{
    [Fact]
    public void Create_ShouldUppercaseSku()
    {
        var item = BasketItem.Create(Guid.NewGuid(), "Widget", "wgt-001", 5m, 2, null);

        item.Sku.Should().Be("WGT-001");
    }

    [Fact]
    public void TotalPrice_ShouldRoundToTwoDecimals()
    {
        var item = BasketItem.Create(Guid.NewGuid(), "Widget", "WGT-001", 3.333m, 3, null);

        item.TotalPrice.Should().Be(10.00m);
    }

    [Fact]
    public void TotalPrice_ZeroQuantity_ShouldBeZero()
    {
        var item = BasketItem.Create(Guid.NewGuid(), "W", "W", 5m, 4, null);

        item.TotalPrice.Should().Be(20m);
    }

    [Fact]
    public void Create_ShouldAssignNewId()
    {
        var a = BasketItem.Create(Guid.NewGuid(), "A", "A", 1m, 1, null);
        var b = BasketItem.Create(Guid.NewGuid(), "B", "B", 1m, 1, null);

        a.Id.Should().NotBe(b.Id);
    }
}
