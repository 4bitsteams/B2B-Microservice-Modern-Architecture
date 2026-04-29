using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Application.Interfaces;

public interface IReviewRepository : IRepository<ReviewEntity, Guid>
{
    Task<ReviewEntity?> GetByCustomerAndProductAsync(Guid customerId, Guid productId, CancellationToken ct = default);
}

public interface IReadReviewRepository : IReadRepository<ReviewEntity, Guid>
{
    Task<PagedList<ReviewEntity>> GetApprovedByProductAsync(Guid productId, int page, int pageSize, CancellationToken ct = default);
    Task<double> GetAverageRatingAsync(Guid productId, CancellationToken ct = default);
    Task<int> GetReviewCountAsync(Guid productId, CancellationToken ct = default);
}
