using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Commands.Login;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Tests.Application;

public sealed class LoginHandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly ITenantRepository _tenantRepo = Substitute.For<ITenantRepository>();
    private readonly IRoleRepository _roleRepo = Substitute.For<IRoleRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly LoginHandler _handler;

    private static readonly Tenant TestTenant = Tenant.Create("Acme", "acme");
    private static readonly Role UserRole = Role.Create(Role.SystemRoles.User);

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(
            _userRepo, _tenantRepo, _roleRepo,
            _tokenService, _hasher, _unitOfWork);
    }

    private static User MakeUser(Guid tenantId) =>
        User.Create("Jane", "Doe", "jane@acme.com", "hashed_pw", tenantId);

    // ──────────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldReturnTokens()
    {
        var user = MakeUser(TestTenant.Id);
        var cmd = new LoginCommand("jane@acme.com", "P@ssw0rd!", "acme");

        _tenantRepo.GetBySlugAsync("acme", default).Returns(TestTenant);
        _userRepo.GetByEmailAsync("jane@acme.com", TestTenant.Id, default).Returns(user);
        _hasher.VerifyAsync("P@ssw0rd!", "hashed_pw", default).Returns(true);
        _roleRepo.GetByUserIdAsync(user.Id, default).Returns([UserRole]);
        _tokenService.GenerateAccessToken(user, Arg.Any<IEnumerable<string>>()).Returns("access-token");
        _tokenService.GenerateRefreshToken().Returns("refresh-token");

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        result.Value.FullName.Should().Be("Jane Doe");
        result.Value.Roles.Should().Contain(Role.SystemRoles.User);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldSaveChanges()
    {
        var user = MakeUser(TestTenant.Id);
        var cmd = new LoginCommand("jane@acme.com", "P@ssw0rd!", "acme");

        _tenantRepo.GetBySlugAsync("acme", default).Returns(TestTenant);
        _userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<Guid>(), default).Returns(user);
        _hasher.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(true);
        _roleRepo.GetByUserIdAsync(user.Id, default).Returns([UserRole]);
        _tokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns("at");
        _tokenService.GenerateRefreshToken().Returns("rt");

        await _handler.Handle(cmd, default);

        await _unitOfWork.Received(1).SaveChangesAsync(default);
    }

    // ──────────────────────────────────────────────
    // Tenant not found
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNonExistentTenant_ShouldReturnNotFound()
    {
        _tenantRepo.GetBySlugAsync("unknown", default).Returns((Tenant?)null);

        var result = await _handler.Handle(
            new LoginCommand("jane@acme.com", "pw", "unknown"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ──────────────────────────────────────────────
    // User not found
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldReturnUnauthorized()
    {
        _tenantRepo.GetBySlugAsync("acme", default).Returns(TestTenant);
        _userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<Guid>(), default).Returns((User?)null);

        var result = await _handler.Handle(
            new LoginCommand("nobody@acme.com", "pw", "acme"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    // ──────────────────────────────────────────────
    // Locked account
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithLockedAccount_ShouldReturnUnauthorized()
    {
        var user = MakeUser(TestTenant.Id);
        for (var i = 0; i < 5; i++) user.RecordFailedLogin(); // locks the account

        _tenantRepo.GetBySlugAsync("acme", default).Returns(TestTenant);
        _userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<Guid>(), default).Returns(user);

        var result = await _handler.Handle(
            new LoginCommand("jane@acme.com", "pw", "acme"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.AccountLocked");
    }

    // ──────────────────────────────────────────────
    // Wrong password
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        var user = MakeUser(TestTenant.Id);

        _tenantRepo.GetBySlugAsync("acme", default).Returns(TestTenant);
        _userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<Guid>(), default).Returns(user);
        _hasher.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(false);

        var result = await _handler.Handle(
            new LoginCommand("jane@acme.com", "wrong!", "acme"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Handle_WithInvalidPassword_ShouldIncrementFailedLoginCounter()
    {
        var user = MakeUser(TestTenant.Id);

        _tenantRepo.GetBySlugAsync("acme", default).Returns(TestTenant);
        _userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<Guid>(), default).Returns(user);
        _hasher.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(false);

        await _handler.Handle(new LoginCommand("jane@acme.com", "wrong!", "acme"), default);

        user.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithInvalidPassword_ShouldSaveFailedAttempt()
    {
        var user = MakeUser(TestTenant.Id);

        _tenantRepo.GetBySlugAsync("acme", default).Returns(TestTenant);
        _userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<Guid>(), default).Returns(user);
        _hasher.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(false);

        await _handler.Handle(new LoginCommand("jane@acme.com", "wrong!", "acme"), default);

        await _unitOfWork.Received(1).SaveChangesAsync(default);
    }
}
