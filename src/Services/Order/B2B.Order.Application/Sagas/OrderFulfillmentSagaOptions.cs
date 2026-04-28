namespace B2B.Order.Application.Sagas;

/// <summary>
/// Configuration for <see cref="OrderFulfillmentSaga"/> timeout deadlines.
///
/// OCP — add or extend timeout values here without modifying the saga state machine.
///
/// Register in DI:
/// <code>
/// services.Configure&lt;OrderFulfillmentSagaOptions&gt;(
///     config.GetSection(OrderFulfillmentSagaOptions.SectionName));
/// </code>
///
/// appsettings.json (all values optional — defaults apply if section is absent):
/// <code>
/// "OrderFulfillmentSaga": {
///   "StockReservationDeadlineMinutes": 5,
///   "PaymentDeadlineMinutes": 10,
///   "ShipmentDeadlineHours": 2
/// }
/// </code>
/// </summary>
public sealed class OrderFulfillmentSagaOptions
{
    public const string SectionName = "OrderFulfillmentSaga";

    /// <summary>
    /// Maximum time the saga waits for a stock reservation reply before cancelling.
    /// Default: 5 minutes.
    /// </summary>
    public int StockReservationDeadlineMinutes { get; init; } = 5;

    /// <summary>
    /// Maximum time the saga waits for a payment reply before cancelling.
    /// Default: 10 minutes.
    /// </summary>
    public int PaymentDeadlineMinutes { get; init; } = 10;

    /// <summary>
    /// Maximum time the saga waits for a shipment reply before cancelling.
    /// Default: 2 hours.
    /// </summary>
    public int ShipmentDeadlineHours { get; init; } = 2;

    // ── Derived TimeSpan accessors ─────────────────────────────────────────────

    public TimeSpan StockReservationDeadline => TimeSpan.FromMinutes(StockReservationDeadlineMinutes);
    public TimeSpan PaymentDeadline          => TimeSpan.FromMinutes(PaymentDeadlineMinutes);
    public TimeSpan ShipmentDeadline         => TimeSpan.FromHours(ShipmentDeadlineHours);
}
