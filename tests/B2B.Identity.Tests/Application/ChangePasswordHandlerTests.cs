using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Commands.ChangePassword;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Tests.Application;

public sealed class ChangePasswordHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly ChangePasswordHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly ChangePasswordCommand ValidCommand =
        new("OldP@ss1!", "NewP@ss2!", "NewP@ss2!");

    public ChangePasswordHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);

        _handler = new ChangePasswordHandler(_userRepo, _hasher, _unitOfWork, _currentUser);
    }

    private static User MakeUser() =>
        User.Create("Alice", "Smith", "alice@acme.com", "old_hash", TenantId);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCurrentPassword_ShouldReturnSuccess()
    {
        var user = MakeUser();
        _userRepo.GetByIdAsync(UserId, default).Returns(user);
        _hasher.VerifyAsync("OldP@ss1!", "old_hash", default).Returns(true);
        _hasher.HashAsync("NewP@ss2!", default).Returns("new_hash");

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidCurrentPassword_ShouldHashNewPassword()
    {
        var user = MakeUser();
        _userRepo.GetByIdAsync(UserId, default).Returns(user);
        _hasher.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(true);
        _hasher.HashAsync("NewP@ss2!", default).Returns("new_hash");

        await _handler.Handle(ValidCommand, default);

        await _hasher.Received(1).HashAsync("NewP@ss2!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidCurrentPassword_ShouldSaveChanges()
    {
        var user = MakeUser();
        _userRepo.GetByIdAsync(UserId, default).Returns(user);
        _hasher.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(true);
        _hasher.HashAsync(Arg.Any<string>(), default).Returns("new_hash");

        await _handler.Handle(ValidCommand, default);

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

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ShouldReturnValidation()
    {
        var user = MakeUser();
        _userRepo.GetByIdAsync(UserId, default).Returns(user);
        _hasher.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(false);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("User.InvalidPassword");
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ShouldNotSaveChanges()
    {
        var user = MakeUser();
        _userRepo.GetByIdAsync(UserId, default).Returns(user);
        _hasher.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(false);

        await _handler.Handle(ValidCommand, default);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(default);
    }
}
