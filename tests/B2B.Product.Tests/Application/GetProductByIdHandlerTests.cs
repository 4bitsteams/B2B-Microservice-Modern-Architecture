using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Product.Application.Interfaces;
using B2B.Product.Application.Queries.GetProductById;
using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Tests.Application;

public sealed class GetProductByIdHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadProductRepository _productRepo = Substitute.For<IReadProductRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetProductByIdHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public GetProductByIdHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        // Default: cache miss
        _cache.GetAsync<B2B.Product.Application.Queries.GetProducts.ProductDto>(
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((B2B.Product.Application.Queries.GetProducts.ProductDto?)null);

        _handler = new GetProductByIdHandler(_productRepo, _cache, _currentUser);
    }

    private static ProductEntity MakeProduct(Guid? tenantId = null) =>
        ProductEntity.Create("Widget Pro", "A widget", "WGT-PRO",
            Money.Of(99.99m, "USD"), 20, CategoryId, tenantId ?? TenantId);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProductExists_ShouldReturnDto()
    {
        var product = MakeProduct();
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Widget Pro");
        result.Value.Sku.Should().Be("WGT-PRO");
        result.Value.Price.Should().Be(99.99m);
    }

    [Fact]
    public async Task Handle_WhenProductExists_ShouldCacheResult()
    {
        var product = MakeProduct();
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        await _handler.Handle(new GetProductByIdQuery(product.Id), default);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<B2B.Product.Application.Queries.GetProducts.ProductDto>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedDto()
    {
        var product = MakeProduct();
        var cachedDto = new B2B.Product.Application.Queries.GetProducts.ProductDto(
            product.Id, "Widget Pro", "A widget", "WGT-PRO",
            99.99m, "USD", 20, true, false, null, null, "Active", DateTime.UtcNow);
        _cache.GetAsync<B2B.Product.Application.Queries.GetProducts.ProductDto>(
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(cachedDto);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(cachedDto);
        // Should NOT hit the database when cache returns a value
        await _productRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProductNotFound_ShouldReturnNotFound()
    {
        _productRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((ProductEntity?)null);

        var result = await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public async Task Handle_WhenProductBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        var product = MakeProduct(tenantId: Guid.NewGuid());
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
