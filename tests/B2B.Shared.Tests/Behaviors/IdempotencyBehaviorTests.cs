using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace B2B.Shared.Tests.Behaviors;

// ── Test request/response stubs ──────────────────────────────────────────────

public sealed record IdempotentTestCommand(string Payload) : IRequest<Result>, IIdempotentCommand
{
    public string IdempotencyKey { get; init; } = string.Empty;
}

// ────────────────────────────────────────────────────────────────────────────

public sealed class IdempotencyBehaviorTests
{
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ILogger<IdempotencyBehavior<IdempotentTestCommand, Result>> _logger =
        Substitute.For<ILogger<IdempotencyBehavior<IdempotentTestCommand, Result>>>();

    private IdempotencyBehavior<IdempotentTestCommand, Result> CreateBehavior() =>
        new(_cache, _logger);

    // ── No idempotency key ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithBlankIdempotencyKey_ShouldCallNextDirectly()
    {
        var behavior = CreateBehavior();
        var command = new IdempotentTestCommand("data") { IdempotencyKey = "" };

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        await behavior.Handle(command, Next, default);

        nextCalled.Should().BeTrue();
        await _cache.DidNotReceive().GetAsync<IdempotencyRecord>(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Cache miss — first execution ────────────────────────────────────────────

    [Fact]
    public async Task Handle_OnFirstCall_ShouldExecuteHandlerAndCacheResult()
    {
        var behavior = CreateBehavior();
        var command = new IdempotentTestCommand("data") { IdempotencyKey = "key-001" };
        _cache.GetAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IdempotencyRecord?)null);

        await behavior.Handle(command, () => Task.FromResult(Result.Success()), default);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyRecord>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    // ── Cache hit — duplicate call ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_OnDuplicateCall_ShouldReturnCachedResultWithoutCallingNext()
    {
        var behavior = CreateBehavior();
        var command = new IdempotentTestCommand("data") { IdempotencyKey = "key-002" };
        var cachedRecord = new IdempotencyRecord(true, Error.None, null);
        _cache.GetAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(cachedRecord);

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        var result = await behavior.Handle(command, Next, default);

        nextCalled.Should().BeFalse();
        result.IsSuccess.Should().BeTrue();
    }

    // ── Failed response not cached ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenHandlerFails_ShouldNotCacheResult()
    {
        var behavior = CreateBehavior();
        var command = new IdempotentTestCommand("bad") { IdempotencyKey = "key-003" };
        _cache.GetAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IdempotencyRecord?)null);

        var failureResult = Result.Failure(Error.Validation("Test.Error", "Something went wrong."));

        await behavior.Handle(command, () => Task.FromResult(failureResult), default);

        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyRecord>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
