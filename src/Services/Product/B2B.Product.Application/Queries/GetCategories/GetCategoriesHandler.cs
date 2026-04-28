using B2B.Product.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Application.Queries.GetCategories;

public sealed class GetCategoriesHandler(
    IReadCategoryRepository categoryRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(
        GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetByTenantAsync(
            currentUser.TenantId, cancellationToken);

        var dtos = categories
            .Select(c => new CategoryDto(
                c.Id, c.Name, c.Slug, c.Description,
                c.ParentCategoryId, c.SortOrder, c.IsActive))
            .ToList();

        return dtos;
    }
}
