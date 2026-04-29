using B2B.Review.Domain.Entities;
using B2B.Review.Domain.Events;
using FluentAssertions;
using Xunit;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Tests.Domain;

public sealed class ReviewTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static ReviewEntity New(int rating = 5, string title = "Great", string body = "Loved it",
        Guid? orderId = null, bool verifiedPurchase = false) =>
        ReviewEntity.Submit(ProductId, CustomerId, TenantId, rating, title, body, orderId, verifiedPurchase);

    [Fact]
    public void Submit_ShouldInitializePending()
    {
        var r = New();

        r.Status.Should().Be(ReviewStatus.Pending);
        r.Rating.Should().Be(5);
        r.HelpfulVotes.Should().Be(0);
    }

    [Fact]
    public void Submit_ShouldRaiseSubmittedEvent()
    {
        var r = New();

        r.DomainEvents.Should().ContainSingle(e => e is ReviewSubmittedEvent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Submit_RatingOutOfRange_ShouldThrow(int rating)
    {
        var act = () => New(rating: rating);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Submit_BlankTitle_ShouldThrow()
    {
        var act = () => New(title: "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Submit_BlankBody_ShouldThrow()
    {
        var act = () => New(body: "");

        act.Should().Throw<ArgumentException>();
    }

    // ── Approve ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_FromPending_ShouldTransitionAndRaiseEvent()
    {
        var r = New();
        r.ClearDomainEvents();

        r.Approve();

        r.Status.Should().Be(ReviewStatus.Approved);
        r.DomainEvents.Should().ContainSingle(e => e is ReviewApprovedEvent);
    }

    [Fact]
    public void Approve_AlreadyApproved_ShouldThrow()
    {
        var r = New();
        r.Approve();

        var act = () => r.Approve();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Reject ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_FromPending_ShouldTransition()
    {
        var r = New();

        r.Reject("spam");

        r.Status.Should().Be(ReviewStatus.Rejected);
    }

    [Fact]
    public void Reject_AlreadyApproved_ShouldThrow()
    {
        var r = New();
        r.Approve();

        var act = () => r.Reject("late");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── MarkHelpful ─────────────────────────────────────────────────────────────

    [Fact]
    public void MarkHelpful_ShouldIncrementVotes()
    {
        var r = New();

        r.MarkHelpful();
        r.MarkHelpful();

        r.HelpfulVotes.Should().Be(2);
    }
}
