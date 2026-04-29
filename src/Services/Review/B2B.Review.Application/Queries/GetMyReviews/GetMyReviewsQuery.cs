using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Review.Application.Queries.GetMyReviews;

public sealed record GetMyReviewsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedList<MyReviewDto>>;

public sealed record MyReviewDto(
    Guid Id,
    Guid ProductId,
    int Rating,
    string Title,
    string Body,
    string Status,
    int HelpfulVotes,
    DateTime CreatedAt);
