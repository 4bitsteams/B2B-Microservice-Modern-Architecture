using B2B.Review.Application.Commands.ApproveReview;
using B2B.Review.Application.Interfaces;
using B2B.Review.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Tests.Application;

public sealed class ApproveReviewHandlerTests
{
    private readonly IReviewRepository _repo = Substitute.For<IReviewRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ApproveReviewHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public ApproveReviewHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new ApproveReviewHandler(_repo, _currentUser, _uow);
    }

    private ReviewEntity MakeReview(Guid? tenantId = null) =>
        ReviewEntity.Submit(Guid.NewGuid(), Guid.NewGuid(), tenantId ?? TenantId, 5, "T", "B");

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ReviewEntity?)null);

        var result = await _handler.Handle(new ApproveReviewCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var r = MakeReview(tenantId: Guid.NewGuid());
        _repo.GetByIdAsync(r.Id, Arg.Any<CancellationToken>()).Returns(r);

        var result = await _handler.Handle(new ApproveReviewCommand(r.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyApproved_ShouldReturnValidation()
    {
        var r = MakeReview();
        r.Approve();
        _repo.GetByIdAsync(r.Id, Arg.Any<CancellationToken>()).Returns(r);

        var result = await _handler.Handle(new ApproveReviewCommand(r.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.InvalidStatus");
    }

    [Fact]
    public async Task Handle_Valid_ShouldApproveAndPersist()
    {
        var r = MakeReview();
        _repo.GetByIdAsync(r.Id, Arg.Any<CancellationToken>()).Returns(r);

        var result = await _handler.Handle(new ApproveReviewCommand(r.Id), default);

        result.IsSuccess.Should().BeTrue();
        r.Status.Should().Be(ReviewStatus.Approved);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
