using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Application.Commands.CreateProduct;

public sealed class CreateProductHandler(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateProductCommand, CreateProductResponse>
{
    public async Task<Result<CreateProductResponse>> Handle(
        CreateProductCommand request, CancellationToken ct)
    {
        var category = await categoryRepository.GetByIdAsync(request.CategoryId, ct);
        if (category is null || category.TenantId != currentUser.TenantId)
            return Error.NotFound("Category.NotFound", "Category not found.");

        var skuExists = await productRepository.GetBySkuAsync(request.Sku, currentUser.TenantId, ct);
        if (skuExists is not null)
            return Error.Conflict("Product.SkuExists", $"A product with SKU '{request.Sku}' already exists.");

        var price = Money.Of(request.Price, request.Currency);
        var product = ProductEntity.Create(
            request.Name, request.Description, request.Sku,
            price, request.StockQuantity, request.CategoryId,
            currentUser.TenantId, request.LowStockThreshold);

        await productRepository.AddAsync(product, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            // Concurrent create with the same SKU arrived between GetBySkuAsync and SaveChanges.
            return Error.Conflict("Product.SkuExists", $"A product with SKU '{request.Sku}' already exists.");
        }

        return new CreateProductResponse(product.Id, product.Name, product.Sku);
    }
}
