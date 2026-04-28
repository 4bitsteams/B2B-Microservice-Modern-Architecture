using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Application.Queries.GetUserProfile;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Tests.Application;

public sealed class GetUserProfileHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadUserRepository _userRepo = Substitute.For<IReadUserRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetUserProfileHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    public GetUserProfileHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);

        _handler = new GetUserProfileHandler(_userRepo, _currentUser);
    }

    private static User MakeUser() =>
        User.Create("Carol", "King", "carol@acme.com", "hash", TenantId);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserExists_ShouldReturnUserSummary()
    {
        var user = MakeUser();
        _userRepo.GetWithRolesAsync(UserId, default).Returns(user);

        var result = await _handler.Handle(new GetUserProfileQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("carol@acme.com");
        result.Value.FullName.Should().Be("Carol King");
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldReflectEmailVerifiedStatus()
    {
        var user = MakeUser();
        user.VerifyEmail();
        _userRepo.GetWithRolesAsync(UserId, default).Returns(user);

        var result = await _handler.Handle(new GetUserProfileQuery(), default);

        result.Value!.EmailVerified.Should().BeTrue();
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnNotFound()
    {
        _userRepo.GetWithRolesAsync(UserId, default).Returns((User?)null);

        var result = await _handler.Handle(new GetUserProfileQuery(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }
}
