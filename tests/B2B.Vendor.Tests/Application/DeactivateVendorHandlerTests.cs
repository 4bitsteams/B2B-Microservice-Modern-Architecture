using B2B.Vendor.Application.Commands.DeactivateVendor;
using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;
using VendorStatus = B2B.Vendor.Domain.Entities.VendorStatus;

namespace B2B.Vendor.Tests.Application;

public sealed class DeactivateVendorHandlerTests
{
    private readonly IVendorRepository _repo = Substitute.For<IVendorRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly DeactivateVendorHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public DeactivateVendorHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new DeactivateVendorHandler(_repo, _currentUser, _uow);
    }

    private VendorEntity NewActive()
    {
        var v = VendorEntity.Register("Acme Corp", "contact@acme.com", "TX-1",
            "1 St", "NYC", "US", TenantId);
        v.Approve(10m);
        return v;
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((VendorEntity?)null);

        var result = await _handler.Handle(new DeactivateVendorCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyDeactivated_ShouldReturnConflict()
    {
        var vendor = NewActive();
        vendor.Deactivate();
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new DeactivateVendorCommand(vendor.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vendor.InvalidState");
    }

    [Fact]
    public async Task Handle_Active_ShouldDeactivateAndPersist()
    {
        var vendor = NewActive();
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new DeactivateVendorCommand(vendor.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Deactivated");
        vendor.Status.Should().Be(VendorStatus.Deactivated);
        _repo.Received(1).Update(vendor);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
