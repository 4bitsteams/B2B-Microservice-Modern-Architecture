using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using B2B.Order.Application.Sagas;
using B2B.Order.Infrastructure.Persistence;
using B2B.Order.Infrastructure.Sagas;

namespace B2B.Order.Infrastructure.Workers;

/// <summary>
/// Background service that periodically identifies and marks saga instances
/// stuck in a terminal-approach state for longer than the configured threshold.
///
/// A saga becomes "stuck" when an external service (stock, payment, shipment)
/// never replies — neither success nor failure — so the saga never reaches
/// Completed or Failed and the row is never cleaned up by SetCompletedWhenFinalized.
///
/// What this worker does:
///   1. Scans for sagas whose <see cref="OrderFulfillmentSagaState.InitiatedAt"/>
///      is older than <see cref="OrderFulfillmentSagaOptions.StockTimeoutMinutes"/> ×
///      StaleMultiplier (default 3× — gives timeout logic three chances before we
///      intervene externally).
///   2. Logs each stuck saga (OrderId, state, age) at Warning level so on-call
///      engineers are alerted via Seq / alerting rules.
///   3. Does NOT mutate the saga state — MassTransit owns saga transitions.
///      If automatic remediation is needed, publish a compensating integration
///      event (e.g. <c>ForceOrderCancellationRequested</c>) here.
///
/// Runs every <see cref="CheckIntervalMinutes"/> minutes (default 5).
/// </summary>
public sealed class StuckSagaCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderFulfillmentSagaOptions> options,
    ILogger<StuckSagaCleanupWorker> logger) : BackgroundService
{
    // How long between scans.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    // How many times the configured saga timeout must have elapsed before we
    // consider a saga stuck. Gives the saga's own timeout scheduling a chance
    // to fire first.
    private const int StaleMultiplier = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StuckSagaCleanupWorker started — scanning every {Interval} minutes",
            CheckInterval.TotalMinutes);

        // Delay first run so the service finishes startup before hitting the DB.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanForStuckSagasAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log but continue — a single scan failure must not crash the worker.
                logger.LogError(ex, "StuckSagaCleanupWorker scan failed");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        logger.LogInformation("StuckSagaCleanupWorker stopped");
    }

    private async Task ScanForStuckSagasAsync(CancellationToken ct)
    {
        var opts = options.Value;

        // Threshold: stock timeout × StaleMultiplier gives the saga's own retry
        // mechanism time to trigger before we flag it as stuck.
        var stockThreshold    = opts.StockReservationDeadline * StaleMultiplier;
        var paymentThreshold  = opts.PaymentDeadline          * StaleMultiplier;
        var shipmentThreshold = opts.ShipmentDeadline         * StaleMultiplier;

        var cutoff = DateTime.UtcNow;

        // Use a fresh scope per scan so the DbContext lifetime matches the scan unit.
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        // Disable the global tenant filter for this background query — the worker
        // must scan ALL tenants, not the current HTTP user's tenant.
        var stuckSagas = await dbContext.OrderFulfillmentSagas
            .IgnoreQueryFilters()
            .Where(s =>
                (s.CurrentState == "AwaitingStockReservation"  && cutoff - s.InitiatedAt > stockThreshold)   ||
                (s.CurrentState == "AwaitingPayment"           && cutoff - s.InitiatedAt > paymentThreshold) ||
                (s.CurrentState == "AwaitingShipment"          && cutoff - s.InitiatedAt > shipmentThreshold))
            .Select(s => new { s.CorrelationId, s.OrderId, s.TenantId, s.CurrentState, s.InitiatedAt })
            .ToListAsync(ct);

        if (stuckSagas.Count == 0)
        {
            logger.LogDebug("StuckSagaCleanupWorker: no stuck sagas found");
            return;
        }

        foreach (var saga in stuckSagas)
        {
            var age = cutoff - saga.InitiatedAt;

            logger.LogWarning(
                "Stuck saga detected: CorrelationId={CorrelationId} OrderId={OrderId} " +
                "TenantId={TenantId} State={State} Age={AgeMinutes:F1} min — " +
                "saga timeout logic should have fired; investigate broker connectivity",
                saga.CorrelationId, saga.OrderId, saga.TenantId, saga.CurrentState, age.TotalMinutes);
        }

        logger.LogWarning("StuckSagaCleanupWorker: {Count} stuck saga(s) flagged this scan", stuckSagas.Count);
    }
}
