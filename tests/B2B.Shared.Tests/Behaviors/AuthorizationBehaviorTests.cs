using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace B2B.Shared.Tests.Behaviors;

// ── Test request/response stubs ──────────────────────────────────────────────

public sealed record SecureCommand(string Data) : IRequest<Result>;

// ────────────────────────────────────────────────────────────────────────────

public sealed class AuthorizationBehaviorTests
{
    private readonly ILogger<AuthorizationBehavior<SecureCommand, Result>> _logger =
        Substitute.For<ILogger<AuthorizationBehavior<SecureCommand, Result>>>();

    // ── No authorizers ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoAuthorizers_ShouldCallNext()
    {
        var behavior = new AuthorizationBehavior<SecureCommand, Result>(
            Enumerable.Empty<IAuthorizer<SecureCommand>>(), _logger);

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        await behavior.Handle(new SecureCommand("data"), Next, default);

        nextCalled.Should().BeTrue();
    }

    // ── Authorization succeeds ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAuthorizerSucceeds_ShouldCallNext()
    {
        var authorizer = Substitute.For<IAuthorizer<SecureCommand>>();
        authorizer.AuthorizeAsync(Arg.Any<SecureCommand>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationResult.Success());

        var behavior = new AuthorizationBehavior<SecureCommand, Result>(
            new[] { authorizer }, _logger);

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        await behavior.Handle(new SecureCommand("data"), Next, default);

        nextCalled.Should().BeTrue();
    }

    // ── Authorization fails ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAuthorizerFails_ShouldNotCallNext()
    {
        var authorizer = Substitute.For<IAuthorizer<SecureCommand>>();
        authorizer.AuthorizeAsync(Arg.Any<SecureCommand>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationResult.Fail("Not allowed."));

        var behavior = new AuthorizationBehavior<SecureCommand, Result>(
            new[] { authorizer }, _logger);

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        await behavior.Handle(new SecureCommand("data"), Next, default);

        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenAuthorizerFails_ShouldReturnForbidden()
    {
        var authorizer = Substitute.For<IAuthorizer<SecureCommand>>();
        authorizer.AuthorizeAsync(Arg.Any<SecureCommand>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationResult.Fail("You shall not pass."));

        var behavior = new AuthorizationBehavior<SecureCommand, Result>(
            new[] { authorizer }, _logger);

        var result = await behavior.Handle(
            new SecureCommand("data"),
            () => Task.FromResult(Result.Success()),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Authorization.Failed");
    }
}
