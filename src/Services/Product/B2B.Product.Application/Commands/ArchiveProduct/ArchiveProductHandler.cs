using B2B.Product.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Application.Commands.ArchiveProduct;

public sealed class ArchiveProductHandler(
    IProductRepository productRepository,  // write — primary DB
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    ICurrentUser currentUser)
    : ICommandHandler<ArchiveProductCommand>
{
    public async Task<Result> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null || product.TenantId != currentUser.TenantId)
            return Error.NotFound("Product.NotFound", $"Product {request.ProductId} not found.");

        product.Archive();
        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cacheService.RemoveByPrefixAsync($"products:tenant:{currentUser.TenantId}", cancellationToken);
        await cacheService.RemoveAsync($"product:{currentUser.TenantId}:{request.ProductId}", cancellationToken);

        return Result.Success();
    }
}
