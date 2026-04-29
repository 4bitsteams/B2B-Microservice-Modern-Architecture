using B2B.Review.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Review.Application.Queries.GetMyReviews;

public sealed class GetMyReviewsHandler(
    IReadReviewRepository readRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetMyReviewsQuery, PagedList<MyReviewDto>>
{
    public async Task<Result<PagedList<MyReviewDto>>> Handle(GetMyReviewsQuery request, CancellationToken cancellationToken)
    {
        var reviews = await readRepository.FindAsync(
            r => r.CustomerId == currentUser.UserId && r.TenantId == currentUser.TenantId,
            cancellationToken);

        var dtos = reviews
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new MyReviewDto(
                r.Id, r.ProductId, r.Rating, r.Title, r.Body,
                r.Status.ToString(), r.HelpfulVotes, r.CreatedAt));

        return PagedList<MyReviewDto>.Create(dtos, request.Page, request.PageSize);
    }
}
