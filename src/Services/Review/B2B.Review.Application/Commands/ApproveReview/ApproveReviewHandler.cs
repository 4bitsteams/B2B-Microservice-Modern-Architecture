using B2B.Review.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Review.Application.Commands.ApproveReview;

public sealed class ApproveReviewHandler(
    IReviewRepository reviewRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveReviewCommand>
{
    public async Task<Result> Handle(ApproveReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await reviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null || review.TenantId != currentUser.TenantId)
            return Error.NotFound("Review.NotFound", $"Review {request.ReviewId} not found.");

        try { review.Approve(); }
        catch (InvalidOperationException ex)
        { return Error.Validation("Review.InvalidStatus", ex.Message); }

        reviewRepository.Update(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
