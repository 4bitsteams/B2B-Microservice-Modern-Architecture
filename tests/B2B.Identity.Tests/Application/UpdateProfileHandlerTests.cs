using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Commands.UpdateProfile;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Tests.Application;

public sealed class UpdateProfileHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly UpdateProfileHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly UpdateProfileCommand ValidCommand =
        new("Bob", "Builder", "+1-555-0001");

    public UpdateProfileHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);

        _handler = new UpdateProfileHandler(_userRepo, _unitOfWork, _currentUser);
    }

    private static User MakeUser() =>
        User.Create("Alice", "Smith", "alice@acme.com", "hash", TenantId);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccess()
    {
        _userRepo.GetByIdAsync(UserId, default).Returns(MakeUser());

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldUpdateUserProfile()
    {
        var user = MakeUser();
        _userRepo.GetByIdAsync(UserId, default).Returns(user);

        await _handler.Handle(ValidCommand, default);

        user.FirstName.Should().Be("Bob");
        user.LastName.Should().Be("Builder");
        user.PhoneNumber.Should().Be("+1-555-0001");
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPersistChanges()
    {
        _userRepo.GetByIdAsync(UserId, default).Returns(MakeUser());

        await _handler.Handle(ValidCommand, default);

        _userRepo.Received(1).Update(Arg.Any<User>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnNotFound()
    {
        _userRepo.GetByIdAsync(UserId, default).Returns((User?)null);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }
}
