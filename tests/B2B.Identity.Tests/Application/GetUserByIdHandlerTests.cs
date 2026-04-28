using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Application.Queries.GetUserById;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Tests.Application;

public sealed class GetUserByIdHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadUserRepository _userRepo = Substitute.For<IReadUserRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetUserByIdHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetUserByIdHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);

        _handler = new GetUserByIdHandler(_userRepo, _currentUser);
    }

    private static User MakeUser(Guid? tenantId = null) =>
        User.Create("Dave", "Reed", "dave@acme.com", "hash", tenantId ?? TenantId);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserExistsInTenant_ShouldReturnSummary()
    {
        var user = MakeUser();
        _userRepo.GetWithRolesAsync(user.Id, default).Returns(user);

        var result = await _handler.Handle(new GetUserByIdQuery(user.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("dave@acme.com");
        result.Value.FullName.Should().Be("Dave Reed");
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnNotFound()
    {
        _userRepo.GetWithRolesAsync(Arg.Any<Guid>(), default).Returns((User?)null);

        var result = await _handler.Handle(new GetUserByIdQuery(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task Handle_WhenUserBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        var user = MakeUser(tenantId: Guid.NewGuid()); // different tenant
        _userRepo.GetWithRolesAsync(user.Id, default).Returns(user);

        var result = await _handler.Handle(new GetUserByIdQuery(user.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
