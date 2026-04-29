using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Review.Application.Queries.GetProductReviews;

public sealed record GetProductReviewsQuery(Guid ProductId, int Page = 1, int PageSize = 20) : IQuery<ProductReviewsDto>;

public sealed record ProductReviewsDto(
    Guid ProductId,
    double AverageRating,
    int ReviewCount,
    PagedList<ReviewDto> Reviews);

public sealed record ReviewDto(
    Guid Id,
    Guid CustomerId,
    int Rating,
    string Title,
    string Body,
    bool IsVerifiedPurchase,
    int HelpfulVotes,
    DateTime CreatedAt);
