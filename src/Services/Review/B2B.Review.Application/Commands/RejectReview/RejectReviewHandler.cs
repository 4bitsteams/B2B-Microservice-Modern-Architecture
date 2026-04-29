using B2B.Review.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Application.Commands.RejectReview;

public sealed class RejectReviewHandler(
    IReviewRepository reviewRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RejectReviewCommand, RejectReviewResponse>
{
    public async Task<Result<RejectReviewResponse>> Handle(RejectReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await reviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null || review.TenantId != currentUser.TenantId)
            return Error.NotFound("Review.NotFound", $"Review {request.ReviewId} not found.");

        try { review.Reject(request.Reason); }
        catch (InvalidOperationException ex)
        { return Error.Conflict("Review.InvalidState", ex.Message); }

        reviewRepository.Update(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RejectReviewResponse(review.Id, review.Status.ToString());
    }
}
