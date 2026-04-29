using B2B.Basket.Domain.Events;
using FluentAssertions;
using Xunit;
using BasketEntity = B2B.Basket.Domain.Entities.Basket;

namespace B2B.Basket.Tests.Domain;

public sealed class BasketTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static BasketEntity NewBasket() => BasketEntity.CreateFor(CustomerId, TenantId);

    // ── Creation ────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateFor_ShouldInitializeIdAndCustomer()
    {
        var basket = NewBasket();

        basket.Id.Should().NotBeEmpty();
        basket.CustomerId.Should().Be(CustomerId);
        basket.TenantId.Should().Be(TenantId);
        basket.Items.Should().BeEmpty();
        basket.TotalItems.Should().Be(0);
        basket.TotalPrice.Should().Be(0m);
    }

    [Fact]
    public void CreateFor_ShouldStampLastModified()
    {
        var basket = NewBasket();

        basket.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    // ── AddItem ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddItem_NewProduct_ShouldAddToItems()
    {
        var basket = NewBasket();
        var productId = Guid.NewGuid();

        basket.AddItem(productId, "Widget", "wgt-001", 10m, 2);

        basket.Items.Should().ContainSingle();
        basket.Items[0].ProductId.Should().Be(productId);
        basket.Items[0].ProductName.Should().Be("Widget");
        basket.Items[0].Sku.Should().Be("WGT-001"); // upper-cased
        basket.Items[0].Quantity.Should().Be(2);
        basket.Items[0].UnitPrice.Should().Be(10m);
    }

    [Fact]
    public void AddItem_NewProduct_ShouldRaiseItemAddedEvent()
    {
        var basket = NewBasket();
        var productId = Guid.NewGuid();

        basket.AddItem(productId, "Widget", "WGT-001", 10m, 3);

        basket.DomainEvents.Should().ContainSingle(e => e is ItemAddedToBasketEvent);
        var evt = (ItemAddedToBasketEvent)basket.DomainEvents[0];
        evt.CustomerId.Should().Be(CustomerId);
        evt.ProductId.Should().Be(productId);
        evt.Quantity.Should().Be(3);
    }

    [Fact]
    public void AddItem_DuplicateProduct_ShouldIncrementQuantity()
    {
        var basket = NewBasket();
        var productId = Guid.NewGuid();
        basket.AddItem(productId, "Widget", "WGT-001", 10m, 2);

        basket.AddItem(productId, "Widget", "WGT-001", 10m, 3);

        basket.Items.Should().ContainSingle();
        basket.Items[0].Quantity.Should().Be(5);
    }

    [Fact]
    public void AddItem_ShouldUpdateLastModified()
    {
        var basket = NewBasket();
        var before = basket.LastModified;
        Thread.Sleep(20);

        basket.AddItem(Guid.NewGuid(), "Widget", "WGT-001", 10m, 1);

        basket.LastModified.Should().BeAfter(before);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_NonPositiveQuantity_ShouldThrow(int quantity)
    {
        var basket = NewBasket();

        var act = () => basket.AddItem(Guid.NewGuid(), "Widget", "WGT-001", 10m, quantity);

        act.Should().Throw<ArgumentException>().WithMessage("*positive*");
    }

    [Fact]
    public void AddItem_NegativePrice_ShouldThrow()
    {
        var basket = NewBasket();

        var act = () => basket.AddItem(Guid.NewGuid(), "Widget", "WGT-001", -1m, 1);

        act.Should().Throw<ArgumentException>().WithMessage("*negative*");
    }

    [Fact]
    public void AddItem_EmptyName_ShouldThrow()
    {
        var basket = NewBasket();

        var act = () => basket.AddItem(Guid.NewGuid(), "", "WGT-001", 10m, 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddItem_EmptySku_ShouldThrow()
    {
        var basket = NewBasket();

        var act = () => basket.AddItem(Guid.NewGuid(), "Widget", "", 10m, 1);

        act.Should().Throw<ArgumentException>();
    }

    // ── TotalPrice / TotalItems ─────────────────────────────────────────────────

    [Fact]
    public void TotalPrice_ShouldSumLineTotals()
    {
        var basket = NewBasket();
        basket.AddItem(Guid.NewGuid(), "A", "A", 10m, 2);
        basket.AddItem(Guid.NewGuid(), "B", "B", 5.50m, 4);

        basket.TotalPrice.Should().Be(20m + 22m);
        basket.TotalItems.Should().Be(6);
    }

    // ── UpdateItemQuantity ──────────────────────────────────────────────────────

    [Fact]
    public void UpdateItemQuantity_ExistingItem_ShouldSetQuantity()
    {
        var basket = NewBasket();
        var productId = Guid.NewGuid();
        basket.AddItem(productId, "Widget", "WGT-001", 10m, 1);

        basket.UpdateItemQuantity(productId, 7);

        basket.Items[0].Quantity.Should().Be(7);
    }

    [Fact]
    public void UpdateItemQuantity_MissingItem_ShouldThrow()
    {
        var basket = NewBasket();

        var act = () => basket.UpdateItemQuantity(Guid.NewGuid(), 5);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void UpdateItemQuantity_NonPositive_ShouldThrow(int quantity)
    {
        var basket = NewBasket();
        var productId = Guid.NewGuid();
        basket.AddItem(productId, "Widget", "WGT-001", 10m, 1);

        var act = () => basket.UpdateItemQuantity(productId, quantity);

        act.Should().Throw<ArgumentException>();
    }

    // ── RemoveItem ──────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveItem_Existing_ShouldRemoveAndRaiseEvent()
    {
        var basket = NewBasket();
        var productId = Guid.NewGuid();
        basket.AddItem(productId, "Widget", "WGT-001", 10m, 1);
        basket.ClearDomainEvents();

        basket.RemoveItem(productId);

        basket.Items.Should().BeEmpty();
        basket.DomainEvents.Should().ContainSingle(e => e is ItemRemovedFromBasketEvent);
    }

    [Fact]
    public void RemoveItem_Missing_ShouldThrow()
    {
        var basket = NewBasket();

        var act = () => basket.RemoveItem(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Clear ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ShouldEmptyItems()
    {
        var basket = NewBasket();
        basket.AddItem(Guid.NewGuid(), "Widget", "WGT-001", 10m, 1);

        basket.Clear();

        basket.Items.Should().BeEmpty();
        basket.TotalPrice.Should().Be(0m);
    }

    // ── Checkout ────────────────────────────────────────────────────────────────

    [Fact]
    public void Checkout_WithItems_ShouldRaiseCheckedOutEvent()
    {
        var basket = NewBasket();
        basket.AddItem(Guid.NewGuid(), "Widget", "WGT-001", 10m, 2);
        basket.ClearDomainEvents();

        basket.Checkout();

        basket.DomainEvents.Should().ContainSingle(e => e is BasketCheckedOutEvent);
        var evt = (BasketCheckedOutEvent)basket.DomainEvents[0];
        evt.CustomerId.Should().Be(CustomerId);
        evt.TenantId.Should().Be(TenantId);
        evt.TotalAmount.Should().Be(20m);
        evt.Items.Should().ContainSingle();
        evt.Items[0].Sku.Should().Be("WGT-001");
    }

    [Fact]
    public void Checkout_EmptyBasket_ShouldThrow()
    {
        var basket = NewBasket();

        var act = () => basket.Checkout();

        act.Should().Throw<InvalidOperationException>().WithMessage("*empty*");
    }
}
