using B2B.Shared.Core.CQRS;

namespace B2B.Product.Application.Commands.CreateCategory;

public sealed record CreateCategoryCommand(
    string Name,
    string Slug,
    string? Description,
    Guid? ParentCategoryId) : ICommand<CreateCategoryResponse>;

public sealed record CreateCategoryResponse(Guid CategoryId, string Name, string Slug);
