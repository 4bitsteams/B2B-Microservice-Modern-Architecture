using B2B.Shipping.Application.Commands.MarkDelivered;
using B2B.Shipping.Application.Interfaces;
using B2B.Shipping.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Shipping.Tests.Application;

public sealed class MarkDeliveredHandlerTests
{
    private readonly IShipmentRepository _repo = Substitute.For<IShipmentRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly MarkDeliveredHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public MarkDeliveredHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new MarkDeliveredHandler(_repo, _currentUser, _uow);
    }

    private Shipment NewShipped()
    {
        var s = Shipment.Create(Guid.NewGuid(), TenantId, "FedEx", "Alice", "1 Main", "NYC", "US", 5m);
        s.Ship();
        return s;
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Shipment?)null);

        var result = await _handler.Handle(new MarkDeliveredCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_NotYetShipped_ShouldReturnValidation()
    {
        var s = Shipment.Create(Guid.NewGuid(), TenantId, "FedEx", "Alice", "1 Main", "NYC", "US", 5m);
        _repo.GetByIdAsync(s.Id, Arg.Any<CancellationToken>()).Returns(s);

        var result = await _handler.Handle(new MarkDeliveredCommand(s.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Shipment.InvalidStatus");
    }

    [Fact]
    public async Task Handle_Shipped_ShouldDeliver()
    {
        var s = NewShipped();
        _repo.GetByIdAsync(s.Id, Arg.Any<CancellationToken>()).Returns(s);

        var result = await _handler.Handle(new MarkDeliveredCommand(s.Id), default);

        result.IsSuccess.Should().BeTrue();
        s.Status.Should().Be(ShipmentStatus.Delivered);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
