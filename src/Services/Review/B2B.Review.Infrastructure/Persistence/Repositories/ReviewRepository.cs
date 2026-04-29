using Microsoft.EntityFrameworkCore;
using B2B.Review.Application.Interfaces;
using B2B.Shared.Infrastructure.Persistence;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository(ReviewDbContext context)
    : BaseRepository<ReviewEntity, Guid, ReviewDbContext>(context), IReviewRepository
{
    public async Task<ReviewEntity?> GetByCustomerAndProductAsync(Guid customerId, Guid productId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(r => r.CustomerId == customerId && r.ProductId == productId, ct);
}
