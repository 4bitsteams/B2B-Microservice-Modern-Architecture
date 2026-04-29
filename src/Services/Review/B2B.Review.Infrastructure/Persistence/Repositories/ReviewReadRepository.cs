using Microsoft.EntityFrameworkCore;
using B2B.Review.Application.Interfaces;
using B2B.Review.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Infrastructure.Persistence.Repositories;

public sealed class ReviewReadRepository(IDbContextFactory<ReviewDbContext> factory)
    : BaseReadRepository<ReviewEntity, Guid, ReviewDbContext>(factory), IReadReviewRepository
{
    public async Task<PagedList<ReviewEntity>> GetApprovedByProductAsync(Guid productId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var query = ctx.Reviews
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.HelpfulVotes).ThenByDescending(r => r.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedList<ReviewEntity>.Create(items, page, pageSize, total);
    }

    public async Task<double> GetAverageRatingAsync(Guid productId, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var ratings = await ctx.Reviews
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved)
            .Select(r => r.Rating).ToListAsync(ct);
        return ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1);
    }

    public async Task<int> GetReviewCountAsync(Guid productId, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Reviews.CountAsync(r => r.ProductId == productId && r.Status == ReviewStatus.Approved, ct);
    }
}
