using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Product.Application.Commands.ArchiveProduct;
using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.Entities;
using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Tests.Application;

public sealed class ArchiveProductHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IProductRepository _productRepo = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly ArchiveProductHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public ArchiveProductHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new ArchiveProductHandler(_productRepo, _unitOfWork, _cache, _currentUser);
    }

    private static ProductEntity MakeProduct(Guid? tenantId = null) =>
        ProductEntity.Create("Widget", "desc", "WGT-1",
            Money.Of(10m, "USD"), 20, CategoryId, tenantId ?? TenantId);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithExistingProduct_ShouldReturnSuccess()
    {
        var product = MakeProduct();
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        var result = await _handler.Handle(new ArchiveProductCommand(product.Id), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithExistingProduct_ShouldArchiveProduct()
    {
        var product = MakeProduct();
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        await _handler.Handle(new ArchiveProductCommand(product.Id), default);

        product.Status.Should().Be(ProductStatus.Archived);
    }

    [Fact]
    public async Task Handle_WithExistingProduct_ShouldInvalidateCache()
    {
        var product = MakeProduct();
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        await _handler.Handle(new ArchiveProductCommand(product.Id), default);

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

        var result = await _handler.Handle(new ArchiveProductCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public async Task Handle_WhenProductBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        var product = MakeProduct(tenantId: Guid.NewGuid());
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        var result = await _handler.Handle(new ArchiveProductCommand(product.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
