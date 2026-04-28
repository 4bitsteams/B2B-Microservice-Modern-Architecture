using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Application.Queries.GetUsers;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Tests.Application;

public sealed class GetUsersHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadUserRepository _userRepo = Substitute.For<IReadUserRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetUsersHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetUsersHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);

        _handler = new GetUsersHandler(_userRepo, _currentUser);
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnPagedUsers()
    {
        var users = new List<User>
        {
            User.Create("Alice", "Smith", "alice@acme.com", "hash", TenantId),
            User.Create("Bob", "Jones", "bob@acme.com", "hash", TenantId)
        };
        var pagedUsers = PagedList<User>.Create(users, 1, 20);
        _userRepo.GetPagedByTenantAsync(TenantId, 1, 20, default).Returns(pagedUsers);

        var result = await _handler.Handle(new GetUsersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldMapUsersToSummaryDtos()
    {
        var user = User.Create("Alice", "Smith", "alice@acme.com", "hash", TenantId);
        var pagedUsers = PagedList<User>.Create(new[] { user }, 1, 20);
        _userRepo.GetPagedByTenantAsync(TenantId, 1, 20, default).Returns(pagedUsers);

        var result = await _handler.Handle(new GetUsersQuery(1, 20), default);

        result.Value!.Items[0].Email.Should().Be("alice@acme.com");
        result.Value.Items[0].FullName.Should().Be("Alice Smith");
        result.Value.Items[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_WithNoUsers_ShouldReturnEmptyPage()
    {
        var emptyPage = PagedList<User>.Create(Enumerable.Empty<User>(), 1, 20);
        _userRepo.GetPagedByTenantAsync(TenantId, 1, 20, default).Returns(emptyPage);

        var result = await _handler.Handle(new GetUsersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}
