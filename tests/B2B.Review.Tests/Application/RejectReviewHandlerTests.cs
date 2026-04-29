using B2B.Review.Application.Commands.RejectReview;
using B2B.Review.Application.Interfaces;
using B2B.Review.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Tests.Application;

public sealed class RejectReviewHandlerTests
{
    private readonly IReviewRepository _repo = Substitute.For<IReviewRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly RejectReviewHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public RejectReviewHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new RejectReviewHandler(_repo, _currentUser, _uow);
    }

    private ReviewEntity NewReview(Guid? tenantId = null) =>
        ReviewEntity.Submit(Guid.NewGuid(), Guid.NewGuid(), tenantId ?? TenantId, 3, "T", "B");

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ReviewEntity?)null);

        var result = await _handler.Handle(new RejectReviewCommand(Guid.NewGuid(), "spam"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyApproved_ShouldReturnConflict()
    {
        var r = NewReview();
        r.Approve();
        _repo.GetByIdAsync(r.Id, Arg.Any<CancellationToken>()).Returns(r);

        var result = await _handler.Handle(new RejectReviewCommand(r.Id, "late"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Review.InvalidState");
    }

    [Fact]
    public async Task Handle_Valid_ShouldReject()
    {
        var r = NewReview();
        _repo.GetByIdAsync(r.Id, Arg.Any<CancellationToken>()).Returns(r);

        var result = await _handler.Handle(new RejectReviewCommand(r.Id, "off-topic"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Rejected");
    }
}
