using B2B.Shared.Core.CQRS;

namespace B2B.Review.Application.Commands.ApproveReview;

public sealed record ApproveReviewCommand(Guid ReviewId) : ICommand;
