using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Domain.Entities;

public sealed class Category : AggregateRoot<Guid>, IAuditableEntity
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public Category? ParentCategory { get; private set; }
    public Guid TenantId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private readonly List<Category> _subCategories = [];
    public IReadOnlyList<Category> SubCategories => _subCategories.AsReadOnly();

    private readonly List<Product> _products = [];
    public IReadOnlyList<Product> Products => _products.AsReadOnly();

    private Category() { }

    public static Category Create(
        string name, string slug, Guid tenantId,
        string? description = null, Guid? parentCategoryId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            Description = description,
            TenantId = tenantId,
            ParentCategoryId = parentCategoryId,
            IsActive = true
        };

    public void Update(string name, string? description, int sortOrder)
    {
        Name = name;
        Description = description;
        SortOrder = sortOrder;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
