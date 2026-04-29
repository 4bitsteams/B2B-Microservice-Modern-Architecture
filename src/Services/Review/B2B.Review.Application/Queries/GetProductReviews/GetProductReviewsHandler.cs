using B2B.Review.Application.Interfaces;
using B2B.Shared.Core.Common;
using MediatR;

namespace B2B.Review.Application.Queries.GetProductReviews;

public sealed class GetProductReviewsHandler(IReadReviewRepository reviewRepository)
    : IRequestHandler<GetProductReviewsQuery, Result<ProductReviewsDto>>
{
    public async Task<Result<ProductReviewsDto>> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken)
    {
        var paged = await reviewRepository.GetApprovedByProductAsync(
            request.ProductId, request.Page, request.PageSize, cancellationToken);

        var avgRating = await reviewRepository.GetAverageRatingAsync(request.ProductId, cancellationToken);
        var count = await reviewRepository.GetReviewCountAsync(request.ProductId, cancellationToken);

        var dtos = paged.Items.Select(r => new ReviewDto(
            r.Id, r.CustomerId, r.Rating, r.Title, r.Body,
            r.IsVerifiedPurchase, r.HelpfulVotes, r.CreatedAt)).ToList();

        var reviewPage = PagedList<ReviewDto>.Create(dtos, request.Page, request.PageSize, paged.TotalCount);

        return new ProductReviewsDto(request.ProductId, avgRating, count, reviewPage);
    }
}
