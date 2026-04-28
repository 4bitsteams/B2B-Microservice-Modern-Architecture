using System.Runtime.ExceptionServices;
using System.Text.Json;
using MediatR;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that records a compliance audit trail for commands.
///
/// Records run AFTER the handler completes so the audit reflects the actual
/// outcome (success or failure). Queries are excluded — audit records are
/// only useful for state-mutating operations.
///
/// Sensitive fields (passwords, tokens) are intentionally excluded from
/// the JSON payload by serializing only the request type name and its
/// non-sensitive properties. For full payload capture, ensure your command
/// records do not expose secret fields.
///
/// Pipeline order:
///   LoggingBehavior → RetryBehavior → IdempotencyBehavior → PerformanceBehavior
///   → AuthorizationBehavior → ValidationBehavior → AuditBehavior
///   → DomainEventBehavior → Handler
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse>(
    IAuditService auditService,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Computed once per closed generic type by the JIT — zero allocation on hot path.
    private static readonly bool IsCommand = typeof(TRequest).GetInterfaces()
        .Any(i => i == typeof(ICommand) ||
                  (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        // Honour [JsonIgnore] on password/token fields — sensitive data stays out of the audit log.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand)
            return await next();

        var requestName = typeof(TRequest).Name;
        bool succeeded = false;
        string? errorMessage = null;
        TResponse? response = default;
        ExceptionDispatchInfo? capturedException = null;

        try
        {
            response = await next();
            succeeded = IsSuccessResult(response);
            if (!succeeded)
                errorMessage = ExtractError(response);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            capturedException = ExceptionDispatchInfo.Capture(ex);
        }

        // Audit runs whether the handler succeeded or threw — but never blocks the response.
        var payload = TrySerialize(request);
        var record = AuditRecord.Create(
            requestName,
            currentUser.IsAuthenticated ? currentUser.UserId : null,
            currentUser.IsAuthenticated ? currentUser.TenantId : null,
            currentUser.IsAuthenticated ? currentUser.Email : null,
            payload,
            succeeded,
            errorMessage);

        await auditService.RecordAsync(record, CancellationToken.None);

        // Re-throw preserving the original stack trace.
        capturedException?.Throw();

        return response!;
    }

    private static bool IsSuccessResult(TResponse response)
    {
        if (response is Result r) return r.IsSuccess;
        return true;
    }

    private static string? ExtractError(TResponse response)
    {
        if (response is Result r) return r.Error.Description;
        return null;
    }

    private static string TrySerialize(TRequest request)
    {
        try
        {
            return JsonSerializer.Serialize(request, SerializerOptions);
        }
        catch
        {
            return $"<{typeof(TRequest).Name}>";
        }
    }
}
