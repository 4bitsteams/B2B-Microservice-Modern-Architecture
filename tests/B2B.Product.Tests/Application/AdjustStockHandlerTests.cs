using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Product.Application.Commands.AdjustStock;
using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Tests.Application;

public sealed class AdjustStockHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IProductRepository _productRepo = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly AdjustStockHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public AdjustStockHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new AdjustStockHandler(_productRepo, _unitOfWork, _cache, _currentUser);
    }

    private static ProductEntity MakeProduct(Guid? tenantId = null, int stock = 50) =>
        ProductEntity.Create("Widget", "desc", "WGT-1",
            Money.Of(10m, "USD"), stock, CategoryId, tenantId ?? TenantId);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccess()
    {
        var product = MakeProduct();
        var cmd = new AdjustStockCommand(product.Id, 10, "Restock");
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithPositiveQuantity_ShouldIncreaseStock()
    {
        var product = MakeProduct(stock: 50);
        var cmd = new AdjustStockCommand(product.Id, 20, "Restock");
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        await _handler.Handle(cmd, default);

        product.StockQuantity.Should().Be(70);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldInvalidateCache()
    {
        var product = MakeProduct();
        var cmd = new AdjustStockCommand(product.Id, 5, "Adjustment");
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        await _handler.Handle(cmd, default);

        await _cache.Received(1).RemoveByPrefixAsync(
            $"products:tenant:{TenantId}", Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(
            $"product:{TenantId}:{product.Id}", Arg.Any<CancellationToken>());
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProductNotFound_ShouldReturnNotFound()
    {
        _productRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((ProductEntity?)null);

        var result = await _handler.Handle(new AdjustStockCommand(Guid.NewGuid(), 5, "test"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public async Task Handle_WhenProductBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        var product = MakeProduct(tenantId: Guid.NewGuid());
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, 5, "test"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
