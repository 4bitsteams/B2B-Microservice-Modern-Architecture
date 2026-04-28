using FluentAssertions;
using Xunit;
using B2B.Product.Domain.ValueObjects;
using B2B.Product.Domain.Events;
using ProductEntity = B2B.Product.Domain.Entities.Product;
using ProductStatus = B2B.Product.Domain.Entities.ProductStatus;

namespace B2B.Product.Tests.Domain;

public sealed class ProductTests
{
    private static ProductEntity CreateValidProduct(int stock = 100) =>
        ProductEntity.Create(
            "Test Product", "Description", "SKU-001",
            Money.Of(99.99m), stock, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var product = CreateValidProduct();

        product.Name.Should().Be("Test Product");
        product.Sku.Should().Be("SKU-001");
        product.Price.Amount.Should().Be(99.99m);
        product.Status.Should().Be(ProductStatus.Active);
        product.IsInStock.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseProductCreatedEvent()
    {
        var product = CreateValidProduct();

        product.DomainEvents.Should().ContainSingle(e => e is ProductCreatedEvent);
        var evt = (ProductCreatedEvent)product.DomainEvents[0];
        evt.ProductId.Should().Be(product.Id);
    }

    [Fact]
    public void UpdatePrice_ShouldSetCompareAtPriceAndRaiseEvent()
    {
        var product = CreateValidProduct();
        var originalPrice = product.Price;
        var newPrice = Money.Of(79.99m);

        product.ClearDomainEvents();
        product.UpdatePrice(newPrice);

        product.Price.Should().Be(newPrice);
        product.CompareAtPrice.Should().Be(originalPrice);
        product.DomainEvents.Should().ContainSingle(e => e is ProductPriceChangedEvent);
    }

    [Fact]
    public void DeductStock_WithSufficientStock_ShouldSucceed()
    {
        var product = CreateValidProduct(50);
        product.ClearDomainEvents();

        product.DeductStock(10);

        product.StockQuantity.Should().Be(40);
    }

    [Fact]
    public void DeductStock_WithInsufficientStock_ShouldThrow()
    {
        var product = CreateValidProduct(5);

        var act = () => product.DeductStock(10);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");
    }

    [Fact]
    public void IsLowStock_WhenBelowThreshold_ShouldBeTrue()
    {
        var product = ProductEntity.Create(
            "Test", "Desc", "SKU-002",
            Money.Of(10m), 5, Guid.NewGuid(), Guid.NewGuid(),
            lowStockThreshold: 10);

        product.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrow()
    {
        var act = () => ProductEntity.Create(
            "", "Description", "SKU-003",
            Money.Of(99.99m), 100, Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }
}
