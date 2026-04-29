using B2B.Discount.Application.Commands.DeactivateDiscount;
using B2B.Discount.Application.Interfaces;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Tests.Application;

public sealed class DeactivateDiscountHandlerTests
{
    private readonly IDiscountRepository _repo = Substitute.For<IDiscountRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly DeactivateDiscountHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DiscountId = Guid.NewGuid();

    public DeactivateDiscountHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new DeactivateDiscountHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(DiscountId, Arg.Any<CancellationToken>()).Returns((DiscountEntity?)null);

        var result = await _handler.Handle(new DeactivateDiscountCommand(DiscountId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var d = DiscountEntity.Create("X", DiscountType.Percentage, 5m, Guid.NewGuid());
        _repo.GetByIdAsync(d.Id, Arg.Any<CancellationToken>()).Returns(d);

        var result = await _handler.Handle(new DeactivateDiscountCommand(d.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyInactive_ShouldReturnConflict()
    {
        var d = DiscountEntity.Create("X", DiscountType.Percentage, 5m, TenantId);
        d.Deactivate();
        _repo.GetByIdAsync(d.Id, Arg.Any<CancellationToken>()).Returns(d);

        var result = await _handler.Handle(new DeactivateDiscountCommand(d.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Discount.AlreadyInactive");
    }

    [Fact]
    public async Task Handle_Active_ShouldDeactivateAndPersist()
    {
        var d = DiscountEntity.Create("X", DiscountType.Percentage, 5m, TenantId);
        _repo.GetByIdAsync(d.Id, Arg.Any<CancellationToken>()).Returns(d);

        var result = await _handler.Handle(new DeactivateDiscountCommand(d.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
