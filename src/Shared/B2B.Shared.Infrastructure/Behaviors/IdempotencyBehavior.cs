using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse>(
    ICacheService cache,
    ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IIdempotentCommand
    where TResponse : Result
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Computed once per TResponse — avoids per-call GetProperty reflection on the cache-write path.
    // Non-null only when TResponse is Result<T>; null for non-generic Result (no value to snapshot).
    private static readonly PropertyInfo? ValueProperty =
        typeof(TResponse).IsGenericType &&
        typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>)
            ? typeof(TResponse).GetProperty(nameof(Result<object>.Value))
            : null;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return await next();

        var cacheKey = $"idem:{typeof(TRequest).FullName}:{request.IdempotencyKey}";

        var cached = await cache.GetAsync<IdempotencyRecord>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            logger.LogInformation(
                "Idempotency hit for {Request} key={Key}",
                typeof(TRequest).Name, request.IdempotencyKey);
            return Reconstruct(cached);
        }

        var response = await next();

        if (response.IsSuccess)
            await cache.SetAsync(cacheKey, Capture(response), DefaultTtl, cancellationToken);

        return response;
    }

    private static IdempotencyRecord Capture(TResponse response)
    {
        JsonElement? value = null;

        if (ValueProperty is not null)
        {
            var raw = ValueProperty.GetValue(response);
            if (raw is not null)
                value = JsonSerializer.SerializeToElement(raw, ValueProperty.PropertyType, JsonOptions);
        }

        return new IdempotencyRecord(response.IsSuccess, response.Error, value);
    }

    private static TResponse Reconstruct(IdempotencyRecord record)
    {
        if (!record.IsSuccess)
            return ResultHelper.Failure<TResponse>(record.Error);

        // Non-generic Result: no value to deserialize.
        if (ValueProperty is null)
            return ResultHelper.Success<TResponse>();

        // Result<T>: deserialize the cached JSON value.
        var innerType = ValueProperty.PropertyType;
        var value = record.Value.HasValue
            ? record.Value.Value.Deserialize(innerType, JsonOptions)
            : null;

        return ResultHelper.Success<TResponse>(value);
    }
}
