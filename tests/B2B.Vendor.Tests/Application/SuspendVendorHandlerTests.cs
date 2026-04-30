using B2B.Vendor.Application.Commands.SuspendVendor;
using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;
using VendorStatus = B2B.Vendor.Domain.Entities.VendorStatus;

namespace B2B.Vendor.Tests.Application;

public sealed class SuspendVendorHandlerTests
{
    private readonly IVendorRepository _repo = Substitute.For<IVendorRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly SuspendVendorHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public SuspendVendorHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new SuspendVendorHandler(_repo, _currentUser, _uow);
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

        var result = await _handler.Handle(new SuspendVendorCommand(Guid.NewGuid(), "Reason"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadySuspended_ShouldReturnValidation()
    {
        var vendor = NewActive();
        vendor.Suspend("First");
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new SuspendVendorCommand(vendor.Id, "Second"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Vendor.InvalidStatus");
    }

    [Fact]
    public async Task Handle_Active_ShouldSuspendAndPersist()
    {
        var vendor = NewActive();
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new SuspendVendorCommand(vendor.Id, "Policy violation"), default);

        result.IsSuccess.Should().BeTrue();
        vendor.Status.Should().Be(VendorStatus.Suspended);
        _repo.Received(1).Update(vendor);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
