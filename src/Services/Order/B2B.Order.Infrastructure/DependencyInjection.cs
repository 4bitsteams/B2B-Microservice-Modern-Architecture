using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Order.Application.Commands.CancelOrder;
using B2B.Order.Application.Interfaces;
using B2B.Order.Application.Sagas;
using B2B.Order.Infrastructure.Messaging;
using B2B.Order.Infrastructure.Persistence;
using B2B.Order.Infrastructure.Persistence.Repositories;
using B2B.Order.Infrastructure.Sagas;
using B2B.Order.Infrastructure.Services;
using B2B.Order.Infrastructure.Workers;
using B2B.Product.Infrastructure.Messaging;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Core.Messaging;
using B2B.Shared.Infrastructure.Extensions;

namespace B2B.Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // ── Database ───────────────────────────────────────────────────────────
        // Registers:
        //   IDbContextFactory<OrderDbContext> (singleton) → read replica, NoTracking
        //   OrderDbContext           (scoped)              → primary, tracking
        services.AddPostgresWithReadReplica<OrderDbContext>(config);
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OrderDbContext>());

        // ── Repositories ───────────────────────────────────────────────────────
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddSingleton<IReadOrderRepository, OrderReadRepository>();

        // ── Order number generation ────────────────────────────────────────────
        // Singleton: stateless, thread-safe, no external dependencies.
        services.AddSingleton<IOrderNumberGenerator, DefaultOrderNumberGenerator>();

        // ── Payment + Shipment gateways ────────────────────────────────────────
        // Stub implementations for development/testing.
        // Replace with real gateway classes (Stripe, FedEx, etc.) for production.
        services.AddScoped<IPaymentGateway, StubPaymentGateway>();
        services.AddScoped<IShipmentGateway, StubShipmentGateway>();

        // ── Authorizers ────────────────────────────────────────────────────────
        // Resource-based authorization run by AuthorizationBehavior before handlers.
        services.AddScoped<IAuthorizer<CancelOrderCommand>, CancelOrderAuthorizer>();

        // ── Saga options ───────────────────────────────────────────────────────
        // Bind from appsettings section; all fields are optional and defaults apply
        // when the section is absent from configuration.
        services.Configure<OrderFulfillmentSagaOptions>(
            config.GetSection(OrderFulfillmentSagaOptions.SectionName));

        // ── Background workers ─────────────────────────────────────────────────
        // Scans for saga instances stuck in intermediate states (e.g. broker outage
        // silenced the reply so the saga timeout never fired). Logs stuck sagas at
        // Warning so on-call engineers are alerted before they become data leaks.
        services.AddHostedService<StuckSagaCleanupWorker>();

        return services;
    }

    /// <summary>
    /// Configures all MassTransit consumers and sagas owned by the Order service.
    ///
    /// DIP — this method is the single point in the codebase that knows about
    /// concrete Infrastructure types (saga, consumers, DbContext).  The caller
    /// (<c>Order.Api/Program.cs</c>) invokes it through an abstraction and never
    /// needs to import <c>B2B.Product.Infrastructure</c> or saga framework types.
    /// </summary>
    public static void ConfigureOrderBusParticipants(IBusRegistrationConfigurator x)
    {
        // ── Saga (orchestrator) ────────────────────────────────────────────────
        x.AddSagaStateMachine<OrderFulfillmentSaga, OrderFulfillmentSagaState>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                r.ExistingDbContext<OrderDbContext>();
            });

        // ── Stock participants (Product service logic) ─────────────────────────
        // These consumers run inside the Order process for single-repo development.
        // In a split-repo deployment, they move to a dedicated Product worker process.
        x.AddConsumer<StockReservationConsumer>();
        x.AddConsumer<ReleaseStockConsumer>();

        // ── Payment participants ───────────────────────────────────────────────
        // Stub — replace with B2B.Payment service consumers in production.
        x.AddConsumer<PaymentConsumer>();
        x.AddConsumer<RefundPaymentConsumer>();

        // ── Shipment participants ──────────────────────────────────────────────
        // Stub — replace with B2B.Shipping service consumers in production.
        x.AddConsumer<ShipmentConsumer>();

    }

    /// <summary>
    /// Configures the Kafka Rider for the Order service.
    ///
    /// Registers:
    ///   • <c>BasketCheckedOutConsumer</c> — consumes from the basket-checked-out topic
    ///     published by the Basket service (cross-process, requires Kafka).
    ///   • Producers for all order-lifecycle integration events consumed by the
    ///     Notification Worker and any other downstream services.
    ///
    /// Called from <c>Order.Api/Program.cs</c> via the <c>AddEventBus(configureRider:)</c>
    /// parameter so <c>Program.cs</c> never needs to import consumer or event types directly.
    /// </summary>
    public static void ConfigureOrderKafkaRider(IRiderRegistrationConfigurator rider, string bootstrapServers)
    {
        // ── Cross-service consumer ─────────────────────────────────────────────
        // BasketCheckedOut arrives from the Basket service via Kafka — it must be on
        // the Rider (not the in-memory bus) because it crosses process boundaries.
        rider.AddConsumer<BasketCheckedOutConsumer>();

        // ── Producers — order lifecycle events ────────────────────────────────
        rider.AddProducer<OrderConfirmedIntegration>(KafkaTopics.OrderConfirmed);
        rider.AddProducer<OrderProcessingStartedIntegration>(KafkaTopics.OrderProcessingStarted);
        rider.AddProducer<OrderPaymentProcessedIntegration>(KafkaTopics.OrderPaymentProcessed);
        rider.AddProducer<OrderFulfilledIntegration>(KafkaTopics.OrderFulfilled);
        rider.AddProducer<OrderCancelledDueToStockIntegration>(KafkaTopics.OrderCancelledStock);
        rider.AddProducer<OrderCancelledDueToPaymentIntegration>(KafkaTopics.OrderCancelledPayment);
        rider.AddProducer<OrderCancelledDueToShipmentIntegration>(KafkaTopics.OrderCancelledShipment);

        // ── Kafka host + topic endpoints ──────────────────────────────────────
        rider.UsingKafka((ctx, k) =>
        {
            k.Host(bootstrapServers);

            k.TopicEndpoint<BasketCheckedOutIntegration>(
                KafkaTopics.BasketCheckedOut, "order-service", e =>
                    e.ConfigureConsumer<BasketCheckedOutConsumer>(ctx));
        });
    }
}
