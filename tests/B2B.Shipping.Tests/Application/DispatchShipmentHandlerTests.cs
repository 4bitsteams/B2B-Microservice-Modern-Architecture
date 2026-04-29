using B2B.Shipping.Application.Commands.DispatchShipment;
using B2B.Shipping.Application.Interfaces;
using B2B.Shipping.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Shipping.Tests.Application;

public sealed class DispatchShipmentHandlerTests
{
    private readonly IShipmentRepository _repo = Substitute.For<IShipmentRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly DispatchShipmentHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public DispatchShipmentHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new DispatchShipmentHandler(_repo, _currentUser, _uow);
    }

    private Shipment NewShipment(Guid? tenant = null) =>
        Shipment.Create(Guid.NewGuid(), tenant ?? TenantId, "FedEx", "Alice", "1 Main", "NYC", "US", 5m);

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Shipment?)null);

        var result = await _handler.Handle(new DispatchShipmentCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var s = NewShipment(tenant: Guid.NewGuid());
        _repo.GetByIdAsync(s.Id, Arg.Any<CancellationToken>()).Returns(s);

        var result = await _handler.Handle(new DispatchShipmentCommand(s.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyShipped_ShouldReturnValidation()
    {
        var s = NewShipment();
        s.Ship();
        _repo.GetByIdAsync(s.Id, Arg.Any<CancellationToken>()).Returns(s);

        var result = await _handler.Handle(new DispatchShipmentCommand(s.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Shipment.InvalidStatus");
    }

    [Fact]
    public async Task Handle_Pending_ShouldShipAndPersist()
    {
        var s = NewShipment();
        _repo.GetByIdAsync(s.Id, Arg.Any<CancellationToken>()).Returns(s);

        var result = await _handler.Handle(new DispatchShipmentCommand(s.Id), default);

        result.IsSuccess.Should().BeTrue();
        s.Status.Should().Be(ShipmentStatus.Shipped);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
