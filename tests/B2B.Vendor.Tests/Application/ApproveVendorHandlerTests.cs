using B2B.Vendor.Application.Commands.ApproveVendor;
using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;
using VendorStatus = B2B.Vendor.Domain.Entities.VendorStatus;

namespace B2B.Vendor.Tests.Application;

public sealed class ApproveVendorHandlerTests
{
    private readonly IVendorRepository _repo = Substitute.For<IVendorRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ApproveVendorHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public ApproveVendorHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new ApproveVendorHandler(_repo, _currentUser, _uow);
    }

    private VendorEntity NewPending() =>
        VendorEntity.Register("Acme Corp", "contact@acme.com", "TX-1",
            "1 St", "NYC", "US", TenantId);

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((VendorEntity?)null);

        var result = await _handler.Handle(new ApproveVendorCommand(Guid.NewGuid(), 10m), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var vendor = VendorEntity.Register("Acme", "a@a.com", "TX-1", "1 St", "NYC", "US", Guid.NewGuid());
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new ApproveVendorCommand(vendor.Id, 10m), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyActive_ShouldReturnValidation()
    {
        var vendor = NewPending();
        vendor.Approve(5m);
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new ApproveVendorCommand(vendor.Id, 10m), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Vendor.InvalidStatus");
    }

    [Fact]
    public async Task Handle_Pending_ShouldApproveAndPersist()
    {
        var vendor = NewPending();
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new ApproveVendorCommand(vendor.Id, 15m), default);

        result.IsSuccess.Should().BeTrue();
        vendor.Status.Should().Be(VendorStatus.Active);
        vendor.CommissionRate.Should().Be(15m);
        _repo.Received(1).Update(vendor);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
