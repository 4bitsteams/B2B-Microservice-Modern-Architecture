using FluentAssertions;
using Xunit;
using B2B.Product.Domain.Entities;

namespace B2B.Product.Tests.Domain;

public sealed class CategoryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // ── Create ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_ShouldSetProperties()
    {
        var category = Category.Create("Electronics", "electronics", TenantId,
            "All electronic products");

        category.Name.Should().Be("Electronics");
        category.Slug.Should().Be("electronics");
        category.TenantId.Should().Be(TenantId);
        category.Description.Should().Be("All electronic products");
        category.IsActive.Should().BeTrue();
        category.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldNormalizeSlugToLowercase()
    {
        var category = Category.Create("My Category", "MY-CATEGORY", TenantId);

        category.Slug.Should().Be("my-category");
    }

    [Fact]
    public void Create_WithParentCategory_ShouldSetParentCategoryId()
    {
        var parentId = Guid.NewGuid();
        var category = Category.Create("Sub", "sub", TenantId, null, parentId);

        category.ParentCategoryId.Should().Be(parentId);
    }

    // ── Update ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ShouldChangeNameDescriptionAndSortOrder()
    {
        var category = Category.Create("Old Name", "old-slug", TenantId);

        category.Update("New Name", "New description", 5);

        category.Name.Should().Be("New Name");
        category.Description.Should().Be("New description");
        category.SortOrder.Should().Be(5);
    }

    // ── Activate / Deactivate ───────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var category = Category.Create("Test", "test", TenantId);

        category.Deactivate();

        category.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_AfterDeactivation_ShouldSetIsActiveTrue()
    {
        var category = Category.Create("Test", "test", TenantId);
        category.Deactivate();

        category.Activate();

        category.IsActive.Should().BeTrue();
    }
}
