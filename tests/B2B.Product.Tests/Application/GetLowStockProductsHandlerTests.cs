using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Product.Application.Interfaces;
using B2B.Product.Application.Queries.GetLowStockProducts;
using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Tests.Application;

public sealed class GetLowStockProductsHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadProductRepository _productRepo = Substitute.For<IReadProductRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetLowStockProductsHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public GetLowStockProductsHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetLowStockProductsHandler(_productRepo, _currentUser);
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnLowStockProducts()
    {
        // Stock of 3 against default threshold of 10 → IsLowStock = true
        var products = new List<ProductEntity>
        {
            ProductEntity.Create("Widget A", "desc", "WGT-A",
                Money.Of(10m, "USD"), 3, CategoryId, TenantId, lowStockThreshold: 10),
            ProductEntity.Create("Widget B", "desc", "WGT-B",
                Money.Of(20m, "USD"), 5, CategoryId, TenantId, lowStockThreshold: 10)
        };
        _productRepo.GetLowStockAsync(TenantId, default).Returns(products);

        var result = await _handler.Handle(new GetLowStockProductsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldMapProductsToDtos()
    {
        var product = ProductEntity.Create("Widget A", "desc", "WGT-A",
            Money.Of(10m, "USD"), 3, CategoryId, TenantId);
        _productRepo.GetLowStockAsync(TenantId, default)
            .Returns(new List<ProductEntity> { product });

        var result = await _handler.Handle(new GetLowStockProductsQuery(), default);

        result.Value!.Should().ContainSingle();
        var dto = result.Value[0];
        dto.Name.Should().Be("Widget A");
        dto.Sku.Should().Be("WGT-A");
    }

    [Fact]
    public async Task Handle_WithNoLowStockProducts_ShouldReturnEmptyList()
    {
        _productRepo.GetLowStockAsync(TenantId, default)
            .Returns(new List<ProductEntity>());

        var result = await _handler.Handle(new GetLowStockProductsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }
}
