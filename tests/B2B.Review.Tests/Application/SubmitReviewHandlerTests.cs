using B2B.Review.Application.Commands.SubmitReview;
using B2B.Review.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using ReviewEntity = B2B.Review.Domain.Entities.Review;

namespace B2B.Review.Tests.Application;

public sealed class SubmitReviewHandlerTests
{
    private readonly IReviewRepository _repo = Substitute.For<IReviewRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly SubmitReviewHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    private static readonly SubmitReviewCommand ValidCommand = new(
        ProductId, Rating: 5, Title: "Great", Body: "Loved it");

    public SubmitReviewHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _repo.GetByCustomerAndProductAsync(UserId, ProductId, Arg.Any<CancellationToken>())
            .Returns((ReviewEntity?)null);
        _handler = new SubmitReviewHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnPending()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_Valid_ShouldPersist()
    {
        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).AddAsync(Arg.Any<ReviewEntity>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateReview_ShouldReturnConflict()
    {
        var existing = ReviewEntity.Submit(ProductId, UserId, TenantId, 4, "ok", "ok");
        _repo.GetByCustomerAndProductAsync(UserId, ProductId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Review.AlreadyExists");
    }

    [Fact]
    public async Task Handle_WithOrderId_ShouldMarkVerifiedPurchase()
    {
        ReviewEntity? captured = null;
        await _repo.AddAsync(Arg.Do<ReviewEntity>(r => captured = r), Arg.Any<CancellationToken>());
        var cmdWithOrder = ValidCommand with { OrderId = Guid.NewGuid() };

        await _handler.Handle(cmdWithOrder, default);

        captured!.IsVerifiedPurchase.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithoutOrderId_ShouldNotBeVerified()
    {
        ReviewEntity? captured = null;
        await _repo.AddAsync(Arg.Do<ReviewEntity>(r => captured = r), Arg.Any<CancellationToken>());

        await _handler.Handle(ValidCommand, default);

        captured!.IsVerifiedPurchase.Should().BeFalse();
    }
}
