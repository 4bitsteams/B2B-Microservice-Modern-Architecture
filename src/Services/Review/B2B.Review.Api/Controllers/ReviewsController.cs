using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Review.Application.Commands.ApproveReview;
using B2B.Review.Application.Commands.RejectReview;
using B2B.Review.Application.Commands.SubmitReview;
using B2B.Review.Application.Queries.GetMyReviews;
using B2B.Review.Application.Queries.GetProductReviews;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Review.Api.Controllers;

[Authorize]
[Route("api/reviews")]
public sealed class ReviewsController(ISender sender) : ApiControllerBase
{
    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetProductReviews(Guid productId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        (await sender.Send(new GetProductReviewsQuery(productId, page, pageSize), ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> SubmitReview(SubmitReviewCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();

    [HttpPost("{reviewId:guid}/approve")]
    public async Task<IActionResult> ApproveReview(Guid reviewId, CancellationToken ct) =>
        (await sender.Send(new ApproveReviewCommand(reviewId), ct)).ToActionResult();

    [HttpGet("my")]
    public async Task<IActionResult> GetMyReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        (await sender.Send(new GetMyReviewsQuery(page, pageSize), ct)).ToActionResult();

    [HttpPost("{reviewId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid reviewId, [FromBody] RejectReviewBody body, CancellationToken ct) =>
        (await sender.Send(new RejectReviewCommand(reviewId, body.Reason), ct)).ToActionResult();
}

public sealed record RejectReviewBody(string Reason);
