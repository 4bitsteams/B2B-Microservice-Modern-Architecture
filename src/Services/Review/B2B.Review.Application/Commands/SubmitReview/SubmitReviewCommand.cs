using B2B.Shared.Core.CQRS;

namespace B2B.Review.Application.Commands.SubmitReview;

public sealed record SubmitReviewCommand(
    Guid ProductId,
    int Rating,
    string Title,
    string Body,
    Guid? OrderId = null) : ICommand<SubmitReviewResponse>;

public sealed record SubmitReviewResponse(Guid ReviewId, string Status);
