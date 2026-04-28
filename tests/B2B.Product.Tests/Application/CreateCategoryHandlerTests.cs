using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Product.Application.Commands.CreateCategory;
using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Tests.Application;

public sealed class CreateCategoryHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly ICategoryRepository _categoryRepo = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly CreateCategoryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly CreateCategoryCommand ValidCommand =
        new("Electronics", "electronics", "All electronic products", null);

    public CreateCategoryHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _categoryRepo.GetBySlugAsync("electronics", TenantId, default)
            .Returns((Category?)null);

        _handler = new CreateCategoryHandler(_categoryRepo, _unitOfWork, _currentUser);
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccess()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Electronics");
        result.Value.Slug.Should().Be("electronics");
        result.Value.CategoryId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPersistCategory()
    {
        await _handler.Handle(ValidCommand, default);

        await _categoryRepo.Received(1).AddAsync(
            Arg.Any<Category>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithParentCategory_ShouldSetParentCategoryId()
    {
        var parentId = Guid.NewGuid();
        var cmd = ValidCommand with { ParentCategoryId = parentId };
        Category? captured = null;
        await _categoryRepo.AddAsync(
            Arg.Do<Category>(c => captured = c), Arg.Any<CancellationToken>());

        await _handler.Handle(cmd, default);

        captured!.ParentCategoryId.Should().Be(parentId);
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSlugAlreadyExists_ShouldReturnConflict()
    {
        var existing = Category.Create("Old Electronics", "electronics", TenantId);
        _categoryRepo.GetBySlugAsync("electronics", TenantId, default).Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Category.SlugExists");
    }
}
