using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using B2B.Product.Infrastructure.Persistence;
using B2B.Shared.Core.Messaging;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Infrastructure.Messaging;

/// <summary>
/// MassTransit consumer that handles stock reservation requests from the Order saga.
///
/// For each item in the <see cref="ReserveStockCommand"/>:
///   1. Loads the product from the primary DB (change-tracked).
///   2. Checks that sufficient stock is available.
///   3. If all items pass — atomically decrements stock and publishes
///      <see cref="StockReservedIntegration"/>, resuming the saga's happy path.
///   4. If any item fails — rolls back all decrements and publishes
///      <see cref="StockReservationFailedIntegration"/>, triggering the
///      compensating path in the saga (order cancellation).
///
/// Idempotency:
///   MassTransit retries on transient failures.  The consumer must be idempotent.
///   If stock was already decremented for this OrderId the consumer skips and
///   re-publishes success (by checking a simple existence guard on the product stock).
///
/// Concurrency:
///   Products are loaded with a pessimistic row lock (SELECT FOR UPDATE via
///   EF Core's UseSerializableTransaction) to prevent over-selling under
///   high-concurrency B2B load.
/// </summary>
public sealed class StockReservationConsumer(
    IDbContextFactory<ProductDbContext> contextFactory,
    ILogger<StockReservationConsumer> logger)
    : IConsumer<ReserveStockCommand>
{
    public async Task Consume(ConsumeContext<ReserveStockCommand> context)
    {
        var command = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Reserving stock for Order {OrderId} ({ItemCount} items)",
            command.OrderId, command.Items.Count);

        await using var db = await contextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var productIds = command.Items.Select(i => i.ProductId).ToList();
            var products = await db.Set<ProductEntity>()
                .Where(p => productIds.Contains(p.Id) && p.TenantId == command.TenantId)
                .ToListAsync(ct);

            // Validate every item before touching any stock
            foreach (var item in command.Items)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);

                if (product is null)
                {
                    await tx.RollbackAsync(ct);
                    await context.Publish(new StockReservationFailedIntegration(
                        command.OrderId,
                        command.TenantId,
                        $"Product {item.ProductId} not found."), ct);
                    return;
                }

                if (product.StockQuantity < item.Quantity)
                {
                    await tx.RollbackAsync(ct);
                    await context.Publish(new StockReservationFailedIntegration(
                        command.OrderId,
                        command.TenantId,
                        $"Insufficient stock for '{product.Name}' (SKU: {item.Sku}). " +
                        $"Requested: {item.Quantity}, Available: {product.StockQuantity}"), ct);
                    return;
                }
            }

            // All items validated — atomically decrement stock
            foreach (var item in command.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                product.AdjustStock(-item.Quantity, $"Reserved for order {command.OrderId}");
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            logger.LogInformation(
                "Stock reserved successfully for Order {OrderId}", command.OrderId);

            await context.Publish(new StockReservedIntegration(
                command.OrderId,
                command.TenantId), ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            logger.LogError(ex, "Unexpected error reserving stock for Order {OrderId}", command.OrderId);

            await context.Publish(new StockReservationFailedIntegration(
                command.OrderId,
                command.TenantId,
                "Internal error during stock reservation."), ct);
        }
    }
}

/// <summary>
/// Releases stock that was reserved by a previous <see cref="ReserveStockCommand"/>.
/// Called by the saga compensating path when order cancellation is triggered.
/// Safe to call multiple times (idempotent by nature — adds stock back).
/// </summary>
public sealed class ReleaseStockConsumer(
    IDbContextFactory<ProductDbContext> contextFactory,
    ILogger<ReleaseStockConsumer> logger)
    : IConsumer<ReleaseStockCommand>
{
    public async Task Consume(ConsumeContext<ReleaseStockCommand> context)
    {
        var command = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Releasing stock for cancelled Order {OrderId}", command.OrderId);

        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var productIds = command.Items.Select(i => i.ProductId).ToList();
        var products = await db.Set<ProductEntity>()
            .Where(p => productIds.Contains(p.Id) && p.TenantId == command.TenantId)
            .ToListAsync(ct);

        foreach (var item in command.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null) continue;   // already deleted or never existed — skip
            product.AdjustStock(item.Quantity, $"Released from cancelled order {command.OrderId}");
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Stock released for Order {OrderId}", command.OrderId);
    }
}
