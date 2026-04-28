using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Behaviors;
using MediatR;

namespace B2B.Shared.Tests.Behaviors;

// ── Test request/response stubs ──────────────────────────────────────────────

public sealed record TestCommand(string Name) : IRequest<Result>;
public sealed record TestCommandWithValue(string Name) : IRequest<Result<string>>;

public sealed class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }
}

// ────────────────────────────────────────────────────────────────────────────

public sealed class ValidationBehaviorTests
{
    // ── No validators ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoValidators_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<TestCommand, Result>(
            Enumerable.Empty<IValidator<TestCommand>>());

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        await behavior.Handle(new TestCommand("test"), Next, default);

        nextCalled.Should().BeTrue();
    }

    // ── Valid request ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidRequest_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<TestCommand, Result>(
            new[] { new TestCommandValidator() });

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        await behavior.Handle(new TestCommand("ValidName"), Next, default);

        nextCalled.Should().BeTrue();
    }

    // ── Invalid request ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldNotCallNext()
    {
        var behavior = new ValidationBehavior<TestCommand, Result>(
            new[] { new TestCommandValidator() });

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        await behavior.Handle(new TestCommand(""), Next, default);

        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldReturnValidationError()
    {
        var behavior = new ValidationBehavior<TestCommand, Result>(
            new[] { new TestCommandValidator() });

        var result = await behavior.Handle(
            new TestCommand(""),
            () => Task.FromResult(Result.Success()),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }
}
