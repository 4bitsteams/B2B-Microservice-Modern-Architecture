using System.Text.Json;
using B2B.Shared.Core.Common;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// Cache payload written by <see cref="IdempotencyBehavior{TRequest,TResponse}"/> for a
/// previously executed idempotent command. Stores enough information to reconstruct the
/// original <see cref="Result"/> or <see cref="Result{TValue}"/> on a replay.
/// </summary>
public sealed record IdempotencyRecord(bool IsSuccess, Error Error, JsonElement? Value);
