using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Product.Application.Interfaces;
using B2B.Product.Application.Queries.GetCategories;
using B2B.Product.Domain.Entities;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Tests.Application;

public sealed class GetCategoriesHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadCategoryRepository _categoryRepo = Substitute.For<IReadCategoryRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetCategoriesHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetCategoriesHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetCategoriesHandler(_categoryRepo, _currentUser);
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnAllCategoriesForTenant()
    {
        var categories = new List<Category>
        {
            Category.Create("Electronics", "electronics", TenantId),
            Category.Create("Clothing", "clothing", TenantId)
        };
        _categoryRepo.GetByTenantAsync(TenantId, default).Returns(categories);

        var result = await _handler.Handle(new GetCategoriesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldMapCategoriesToDtos()
    {
        var category = Category.Create("Electronics", "electronics", TenantId, "Desc");
        _categoryRepo.GetByTenantAsync(TenantId, default)
            .Returns(new List<Category> { category });

        var result = await _handler.Handle(new GetCategoriesQuery(), default);

        result.Value!.Should().ContainSingle();
        var dto = result.Value[0];
        dto.Name.Should().Be("Electronics");
        dto.Slug.Should().Be("electronics");
        dto.Description.Should().Be("Desc");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNoCategories_ShouldReturnEmptyList()
    {
        _categoryRepo.GetByTenantAsync(TenantId, default).Returns(new List<Category>());

        var result = await _handler.Handle(new GetCategoriesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }
}
