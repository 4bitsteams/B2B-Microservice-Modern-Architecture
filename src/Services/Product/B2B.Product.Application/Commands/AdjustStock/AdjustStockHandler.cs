using B2B.Product.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Application.Commands.AdjustStock;

public sealed class AdjustStockHandler(
    IProductRepository productRepository,  // write — primary DB
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    ICurrentUser currentUser)
    : ICommandHandler<AdjustStockCommand>
{
    public async Task<Result> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null || product.TenantId != currentUser.TenantId)
            return Error.NotFound("Product.NotFound", $"Product {request.ProductId} not found.");

        product.AdjustStock(request.Quantity, request.Reason);
        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cached product entries for this tenant
        await cacheService.RemoveByPrefixAsync($"products:tenant:{currentUser.TenantId}", cancellationToken);
        await cacheService.RemoveAsync($"product:{currentUser.TenantId}:{request.ProductId}", cancellationToken);

        return Result.Success();
    }
}
