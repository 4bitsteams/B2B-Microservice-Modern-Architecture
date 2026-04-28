using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Commands.Login;
using B2B.Identity.Application.Commands.RefreshToken;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Tests.Application;

public sealed class RefreshTokenHandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roleRepo = Substitute.For<IRoleRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly RefreshTokenHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Role UserRole = Role.Create(Role.SystemRoles.User);

    public RefreshTokenHandlerTests()
    {
        _handler = new RefreshTokenHandler(
            _userRepo, _roleRepo, _tokenService, _unitOfWork);
    }

    private static User MakeUserWithToken(string tokenValue, DateTime? expiry = null)
    {
        var user = User.Create("Jane", "Doe", "jane@acme.com", "pw", TenantId);
        user.AddRefreshToken(tokenValue, expiry ?? DateTime.UtcNow.AddDays(7));
        return user;
    }

    // ──────────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidToken_ShouldReturnNewTokenPair()
    {
        var user = MakeUserWithToken("old-refresh-token");
        var cmd = new RefreshTokenCommand("old-access-token", "old-refresh-token");

        _tokenService.ValidateToken("old-access-token").Returns((UserId, TenantId));
        _userRepo.GetWithRefreshTokensAsync(UserId, default).Returns(user);
        _roleRepo.GetByUserIdAsync(user.Id, default).Returns([UserRole]);
        _tokenService.GenerateAccessToken(user, Arg.Any<IEnumerable<string>>()).Returns("new-access");
        _tokenService.GenerateRefreshToken().Returns("new-refresh");

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("new-access");
        result.Value.RefreshToken.Should().Be("new-refresh");
    }

    [Fact]
    public async Task Handle_WithValidToken_ShouldRevokeOldRefreshToken()
    {
        var user = MakeUserWithToken("old-refresh-token");
        var cmd = new RefreshTokenCommand("old-access-token", "old-refresh-token");

        _tokenService.ValidateToken("old-access-token").Returns((UserId, TenantId));
        _userRepo.GetWithRefreshTokensAsync(UserId, default).Returns(user);
        _roleRepo.GetByUserIdAsync(user.Id, default).Returns([UserRole]);
        _tokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns("new-access");
        _tokenService.GenerateRefreshToken().Returns("new-refresh");

        await _handler.Handle(cmd, default);

        user.RefreshTokens.Should().NotContain(t => t.Token == "old-refresh-token");
    }

    [Fact]
    public async Task Handle_WithValidToken_ShouldSaveChanges()
    {
        var user = MakeUserWithToken("old-refresh-token");
        var cmd = new RefreshTokenCommand("old-access-token", "old-refresh-token");

        _tokenService.ValidateToken("old-access-token").Returns((UserId, TenantId));
        _userRepo.GetWithRefreshTokensAsync(UserId, default).Returns(user);
        _roleRepo.GetByUserIdAsync(user.Id, default).Returns([UserRole]);
        _tokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns("at");
        _tokenService.GenerateRefreshToken().Returns("rt");

        await _handler.Handle(cmd, default);

        await _unitOfWork.Received(1).SaveChangesAsync(default);
    }

    // ──────────────────────────────────────────────
    // User not found
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnUnauthorized()
    {
        _tokenService.ValidateToken(Arg.Any<string>()).Returns((UserId, TenantId));
        _userRepo.GetWithRefreshTokensAsync(UserId, default).Returns((User?)null);

        var result = await _handler.Handle(
            new RefreshTokenCommand("at", "rt"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.InvalidToken");
    }

    // ──────────────────────────────────────────────
    // Invalid refresh token
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNonExistentRefreshToken_ShouldReturnUnauthorized()
    {
        var user = MakeUserWithToken("different-token");
        _tokenService.ValidateToken(Arg.Any<string>()).Returns((UserId, TenantId));
        _userRepo.GetWithRefreshTokensAsync(UserId, default).Returns(user);

        var result = await _handler.Handle(
            new RefreshTokenCommand("at", "unknown-token"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Handle_WithRevokedRefreshToken_ShouldReturnUnauthorized()
    {
        var user = MakeUserWithToken("my-token");
        user.RevokeRefreshToken("my-token");

        _tokenService.ValidateToken(Arg.Any<string>()).Returns((UserId, TenantId));
        _userRepo.GetWithRefreshTokensAsync(UserId, default).Returns(user);

        var result = await _handler.Handle(
            new RefreshTokenCommand("at", "my-token"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Handle_WithExpiredRefreshToken_ShouldReturnUnauthorized()
    {
        var user = MakeUserWithToken("expired-token", DateTime.UtcNow.AddDays(-1));

        _tokenService.ValidateToken(Arg.Any<string>()).Returns((UserId, TenantId));
        _userRepo.GetWithRefreshTokensAsync(UserId, default).Returns(user);

        var result = await _handler.Handle(
            new RefreshTokenCommand("at", "expired-token"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
    }
}
