using B2B.Review.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Application.Commands.SubmitReview;

public sealed class SubmitReviewHandler(
    IReviewRepository reviewRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SubmitReviewCommand, SubmitReviewResponse>
{
    public async Task<Result<SubmitReviewResponse>> Handle(SubmitReviewCommand request, CancellationToken cancellationToken)
    {
        var existing = await reviewRepository.GetByCustomerAndProductAsync(
            currentUser.UserId, request.ProductId, cancellationToken);
        if (existing is not null)
            return Error.Conflict("Review.AlreadyExists", "You have already reviewed this product.");

        var review = ReviewEntity.Submit(
            request.ProductId, currentUser.UserId, currentUser.TenantId,
            request.Rating, request.Title, request.Body,
            request.OrderId, request.OrderId.HasValue);

        await reviewRepository.AddAsync(review, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubmitReviewResponse(review.Id, review.Status.ToString());
    }
}
