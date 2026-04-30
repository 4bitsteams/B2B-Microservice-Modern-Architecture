using B2B.Vendor.Application.Commands.UpdateVendorProfile;
using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Tests.Application;

public sealed class UpdateVendorProfileHandlerTests
{
    private readonly IVendorRepository _repo = Substitute.For<IVendorRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly UpdateVendorProfileHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public UpdateVendorProfileHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new UpdateVendorProfileHandler(_repo, _currentUser, _uow);
    }

    private VendorEntity NewVendor() =>
        VendorEntity.Register("Acme Corp", "contact@acme.com", "TX-1",
            "1 St", "NYC", "US", TenantId);

    private static UpdateVendorProfileCommand MakeCommand(Guid vendorId) =>
        new(vendorId, "New Name", "new@example.com", "+1234",
            "2 Ave", "LA", "US", "https://new.com", "Description");

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((VendorEntity?)null);

        var result = await _handler.Handle(MakeCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var vendor = VendorEntity.Register("Acme", "a@a.com", "TX-1", "1 St", "NYC", "US", Guid.NewGuid());
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(MakeCommand(vendor.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Valid_ShouldUpdateAndPersist()
    {
        var vendor = NewVendor();
        _repo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(MakeCommand(vendor.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().Be("New Name");
        vendor.CompanyName.Should().Be("New Name");
        vendor.City.Should().Be("LA");
        _repo.Received(1).Update(vendor);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
