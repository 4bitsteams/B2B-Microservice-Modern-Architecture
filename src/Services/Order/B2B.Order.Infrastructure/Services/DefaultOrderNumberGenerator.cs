using B2B.Order.Application.Interfaces;

namespace B2B.Order.Infrastructure.Services;

/// <summary>
/// Default <see cref="IOrderNumberGenerator"/> implementation.
///
/// Format: <c>ORD-{yyyyMMdd}-{8-char hex suffix}</c>
/// Example: <c>ORD-20260428-A3F7C12E</c>
///
/// Thread-safe — uses <see cref="Guid.NewGuid"/> for the suffix.
/// Registered as Singleton — no mutable state.
/// </summary>
public sealed class DefaultOrderNumberGenerator : IOrderNumberGenerator
{
    public string Generate() =>
        $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
