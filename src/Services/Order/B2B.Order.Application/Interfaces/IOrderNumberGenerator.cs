namespace B2B.Order.Application.Interfaces;

/// <summary>
/// Generates unique, human-readable order numbers.
///
/// DIP — <see cref="CreateOrderHandler"/> depends on this abstraction; the
/// implementation lives in Infrastructure and can be swapped (e.g. sequential
/// database-backed numbers, tenant-prefixed formats) without touching the handler.
///
/// Testability — inject a deterministic stub in unit tests to produce predictable
/// order numbers instead of random GUIDs.
/// </summary>
public interface IOrderNumberGenerator
{
    /// <summary>Returns a new unique order number string.</summary>
    string Generate();
}
