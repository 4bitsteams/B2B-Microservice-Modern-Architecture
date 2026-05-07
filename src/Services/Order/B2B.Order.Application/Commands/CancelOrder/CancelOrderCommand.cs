using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.CancelOrder;

/// <summary>
/// Command that cancels an existing order from any non-terminal status
/// (Pending, Confirmed, Processing, or Shipped).
///
/// <para>
/// Authorization is checked by <see cref="CancelOrderAuthorizer"/> before the handler
/// runs: TenantAdmin / SuperAdmin roles can cancel any order within the tenant;
/// regular users may only cancel their own orders.
/// </para>
/// </summary>
/// <param name="OrderId">Identifier of the order to cancel.</param>
/// <param name="Reason">
/// Mandatory human-readable explanation for the cancellation (max 500 chars).
/// Stored on the order and surfaced in cancellation notifications.
/// </param>
public sealed record CancelOrderCommand(Guid OrderId, string Reason) : ICommand;
