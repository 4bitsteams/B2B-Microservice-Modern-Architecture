using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Application.Commands.UpdateProduct;

public sealed class UpdateProductHandler(
    IProductRepository productRepository,  // write — primary DB
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null || product.TenantId != currentUser.TenantId)
            return Error.NotFound("Product.NotFound", $"Product {request.ProductId} not found.");

        product.Update(request.Name, request.Description, request.ImageUrl, request.Tags);
        product.UpdatePrice(Money.Of(request.Price, request.Currency));

        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
