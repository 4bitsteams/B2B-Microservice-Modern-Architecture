using B2B.Shared.Core.CQRS;

namespace B2B.Review.Application.Commands.RejectReview;

public sealed record RejectReviewCommand(Guid ReviewId, string Reason) : ICommand<RejectReviewResponse>;

public sealed record RejectReviewResponse(Guid ReviewId, string Status);
