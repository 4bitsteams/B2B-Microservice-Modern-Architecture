using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Product.Application.Commands.UpdateProduct;
using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.Entities;
using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Tests.Application;

public sealed class UpdateProductHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IProductRepository _productRepo = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly UpdateProductHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public UpdateProductHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new UpdateProductHandler(_productRepo, _unitOfWork, _currentUser);
    }

    private static ProductEntity MakeProduct(Guid? tenantId = null) =>
        ProductEntity.Create("Widget", "desc", "WGT-1",
            Money.Of(10m, "USD"), 20, CategoryId, tenantId ?? TenantId);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccess()
    {
        var product = MakeProduct();
        var cmd = new UpdateProductCommand(product.Id, "Updated Widget", "New desc", 149.99m, "USD", null, null);
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldUpdateProductData()
    {
        var product = MakeProduct();
        var cmd = new UpdateProductCommand(product.Id, "Updated Widget", "New desc", 149.99m, "USD", "img.jpg", "tag1");
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        await _handler.Handle(cmd, default);

        product.Name.Should().Be("Updated Widget");
        product.Description.Should().Be("New desc");
        product.Price.Amount.Should().Be(149.99m);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPersistChanges()
    {
        var product = MakeProduct();
        var cmd = new UpdateProductCommand(product.Id, "Updated", "desc", 50m, "USD", null, null);
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        await _handler.Handle(cmd, default);

        _productRepo.Received(1).Update(product);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProductNotFound_ShouldReturnNotFound()
    {
        _productRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((ProductEntity?)null);

        var cmd = new UpdateProductCommand(Guid.NewGuid(), "Name", "desc", 10m, "USD", null, null);
        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public async Task Handle_WhenProductBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        var product = MakeProduct(tenantId: Guid.NewGuid());
        var cmd = new UpdateProductCommand(product.Id, "Name", "desc", 10m, "USD", null, null);
        _productRepo.GetByIdAsync(product.Id, default).Returns(product);

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
