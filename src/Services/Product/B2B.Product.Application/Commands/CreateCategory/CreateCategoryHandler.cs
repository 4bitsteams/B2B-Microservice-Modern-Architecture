using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Application.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<CreateCategoryCommand, CreateCategoryResponse>
{
    public async Task<Result<CreateCategoryResponse>> Handle(
        CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var slugExists = await categoryRepository.GetBySlugAsync(
            request.Slug, currentUser.TenantId, cancellationToken);

        if (slugExists is not null)
            return Error.Conflict("Category.SlugExists", $"Category slug '{request.Slug}' already exists.");

        var category = Category.Create(
            request.Name, request.Slug,
            currentUser.TenantId,
            request.Description,
            request.ParentCategoryId);

        await categoryRepository.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateCategoryResponse(category.Id, category.Name, category.Slug);
    }
}
