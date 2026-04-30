using B2B.Vendor.Application.Commands.ReactivateVendor;
using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;
using VendorStatus = B2B.Vendor.Domain.Entities.VendorStatus;

namespace B2B.Vendor.Tests.Application;

public sealed class ReactivateVendorHandlerTests
{
    private readonly IVendorRepository _repo = Substitute.For<IVendorRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ReactivateVendorHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public ReactivateVendorHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new ReactivateVendorHandler(_repo, _currentUser, _uow);
    }

    private VendorEntity NewSuspended()
    {
        var v = VendorEntity.Register("Acme Corp", "contact@acme.com", "TX-1",
            "1 St", "NYC", "US", TenantId);
        v.Approve(10m);
        v.Suspend("Reason");
        return v;
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((VendorEntity?)null);

        var result = await _handler.Handle(new ReactivateVendorCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_NotSuspended_ShouldReturnConflict()
    {
        var vendor = VendorEntity.Register("Acme", "a@a.com", "TX-1", "1 St", "NYC", "US", TenantId);
        vendor.Approve(5m);
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new ReactivateVendorCommand(vendor.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vendor.InvalidState");
    }

    [Fact]
    public async Task Handle_Suspended_ShouldReactivateAndPersist()
    {
        var vendor = NewSuspended();
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new ReactivateVendorCommand(vendor.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Active");
        vendor.Status.Should().Be(VendorStatus.Active);
        _repo.Received(1).Update(vendor);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
