using B2B.Review.Application.Interfaces;
using B2B.Review.Application.Queries.GetProductReviews;
using B2B.Shared.Core.Common;
using FluentAssertions;
using NSubstitute;
using Xunit;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Tests.Application;

public sealed class GetProductReviewsHandlerTests
{
    private readonly IReadReviewRepository _repo = Substitute.For<IReadReviewRepository>();
    private readonly GetProductReviewsHandler _handler;

    private static readonly Guid ProductId = Guid.NewGuid();

    public GetProductReviewsHandlerTests()
    {
        _handler = new GetProductReviewsHandler(_repo);
    }

    [Fact]
    public async Task Handle_ShouldReturnAggregateAndDtos()
    {
        var r1 = ReviewEntity.Submit(ProductId, Guid.NewGuid(), Guid.NewGuid(), 5, "Excellent", "Body 1");
        r1.Approve();
        var r2 = ReviewEntity.Submit(ProductId, Guid.NewGuid(), Guid.NewGuid(), 4, "Good", "Body 2");
        r2.Approve();
        var paged = PagedList<ReviewEntity>.Create(new[] { r1, r2 }, 1, 20);

        _repo.GetApprovedByProductAsync(ProductId, 1, 20, Arg.Any<CancellationToken>()).Returns(paged);
        _repo.GetAverageRatingAsync(ProductId, Arg.Any<CancellationToken>()).Returns(4.5);
        _repo.GetReviewCountAsync(ProductId, Arg.Any<CancellationToken>()).Returns(2);

        var result = await _handler.Handle(new GetProductReviewsQuery(ProductId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AverageRating.Should().Be(4.5);
        result.Value.ReviewCount.Should().Be(2);
        result.Value.Reviews.Items.Should().HaveCount(2);
        result.Value.Reviews.Items[0].Title.Should().Be("Excellent");
    }

    [Fact]
    public async Task Handle_NoReviews_ShouldReturnEmpty()
    {
        _repo.GetApprovedByProductAsync(ProductId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(PagedList<ReviewEntity>.Create(Array.Empty<ReviewEntity>(), 1, 20));
        _repo.GetAverageRatingAsync(ProductId, Arg.Any<CancellationToken>()).Returns(0d);
        _repo.GetReviewCountAsync(ProductId, Arg.Any<CancellationToken>()).Returns(0);

        var result = await _handler.Handle(new GetProductReviewsQuery(ProductId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reviews.Items.Should().BeEmpty();
        result.Value.ReviewCount.Should().Be(0);
    }
}
