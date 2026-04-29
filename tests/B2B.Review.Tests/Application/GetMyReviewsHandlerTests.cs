using System.Linq.Expressions;
using B2B.Review.Application.Interfaces;
using B2B.Review.Application.Queries.GetMyReviews;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Tests.Application;

public sealed class GetMyReviewsHandlerTests
{
    private readonly IReadReviewRepository _repo = Substitute.For<IReadReviewRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetMyReviewsHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    public GetMyReviewsHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetMyReviewsHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_NoReviews_ShouldReturnEmpty()
    {
        _repo.FindAsync(Arg.Any<Expression<Func<ReviewEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ReviewEntity>)Array.Empty<ReviewEntity>());

        var result = await _handler.Handle(new GetMyReviewsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOrderByCreatedAtDescending()
    {
        var older = ReviewEntity.Submit(Guid.NewGuid(), UserId, TenantId, 3, "Older", "x");
        var newer = ReviewEntity.Submit(Guid.NewGuid(), UserId, TenantId, 5, "Newer", "y");
        older.CreatedAt = DateTime.UtcNow.AddDays(-2);
        newer.CreatedAt = DateTime.UtcNow;
        _repo.FindAsync(Arg.Any<Expression<Func<ReviewEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ReviewEntity>)new[] { older, newer });

        var result = await _handler.Handle(new GetMyReviewsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Title.Should().Be("Newer");
        result.Value.Items[1].Title.Should().Be("Older");
    }
}
