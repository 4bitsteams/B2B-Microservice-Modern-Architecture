using B2B.Shipping.Application.Commands.CreateShipment;
using B2B.Shipping.Application.Interfaces;
using B2B.Shipping.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Shipping.Tests.Application;

public sealed class CreateShipmentHandlerTests
{
    private readonly IShipmentRepository _repo = Substitute.For<IShipmentRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateShipmentHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly CreateShipmentCommand ValidCommand = new(
        OrderId: Guid.NewGuid(),
        Carrier: "FedEx",
        RecipientName: "Alice",
        Address: "1 Main",
        City: "NYC",
        Country: "US",
        ShippingCost: 9.99m);

    public CreateShipmentHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _repo.GetByOrderIdAsync(ValidCommand.OrderId, Arg.Any<CancellationToken>()).Returns((Shipment?)null);
        _handler = new CreateShipmentHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnPendingWithTrackingNumber()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
        result.Value.TrackingNumber.Should().StartWith("B2B-");
    }

    [Fact]
    public async Task Handle_DuplicateOrder_ShouldReturnConflict()
    {
        var existing = Shipment.Create(ValidCommand.OrderId, TenantId, "UPS", "Bob", "x", "x", "US", 5m);
        _repo.GetByOrderIdAsync(ValidCommand.OrderId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Shipment.AlreadyExists");
    }

    [Fact]
    public async Task Handle_Valid_ShouldPersist()
    {
        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).AddAsync(Arg.Any<Shipment>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
