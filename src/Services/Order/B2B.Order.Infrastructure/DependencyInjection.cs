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
using B2B.Product.Infrastructure.Messaging;
using B2B.Shared.Core.Interfaces;
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

        // ── Basket checkout → Order creation ──────────────────────────────────
        x.AddConsumer<BasketCheckedOutConsumer>();
    }
}
