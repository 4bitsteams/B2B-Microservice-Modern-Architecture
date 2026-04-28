using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using B2B.Product.Application.Commands.CreateProduct;
using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Tests.Application;

public sealed class CreateProductHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IProductRepository _productRepo = Substitute.For<IProductRepository>();
    private readonly ICategoryRepository _categoryRepo = Substitute.For<ICategoryRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly CreateProductHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static readonly CreateProductCommand ValidCommand = new(
        "Widget Pro", "A great widget", "WGT-001",
        99.99m, "USD", 50, CategoryId);

    private static Category MakeCategory(Guid? tenantId = null) =>
        Category.Create("Electronics", "electronics", tenantId ?? TenantId);

    public CreateProductHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);

        // Default happy-path stubs
        _categoryRepo.GetByIdAsync(CategoryId, default).Returns(MakeCategory());
        _productRepo.GetBySkuAsync("WGT-001", TenantId, default).Returns((ProductEntity?)null);

        _handler = new CreateProductHandler(_productRepo, _categoryRepo, _currentUser, _unitOfWork);
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccess()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Widget Pro");
        result.Value.Sku.Should().Be("WGT-001");
        result.Value.ProductId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPersistProduct()
    {
        await _handler.Handle(ValidCommand, default);

        await _productRepo.Received(1).AddAsync(
            Arg.Any<ProductEntity>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCategoryNotFound_ShouldReturnNotFound()
    {
        _categoryRepo.GetByIdAsync(CategoryId, default).Returns((Category?)null);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Category.NotFound");
    }

    [Fact]
    public async Task Handle_WhenCategoryBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        _categoryRepo.GetByIdAsync(CategoryId, default)
            .Returns(MakeCategory(tenantId: Guid.NewGuid()));

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenSkuAlreadyExists_ShouldReturnConflict()
    {
        var existing = ProductEntity.Create("Old Widget", "desc", "WGT-001",
            B2B.Product.Domain.ValueObjects.Money.Of(10m, "USD"), 5, CategoryId, TenantId);
        _productRepo.GetBySkuAsync("WGT-001", TenantId, default).Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Product.SkuExists");
    }

    [Fact]
    public async Task Handle_WhenConcurrentSkuConflict_ShouldReturnConflict()
    {
        _unitOfWork.SaveChangesAsync(default)
            .ThrowsAsync(new UniqueConstraintException("duplicate sku"));

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Product.SkuExists");
    }
}
