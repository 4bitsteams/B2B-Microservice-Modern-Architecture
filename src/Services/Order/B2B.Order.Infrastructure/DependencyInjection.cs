using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Order.Application.Commands.CancelOrder;
using B2B.Order.Application.Interfaces;
using B2B.Order.Application.Sagas;
using B2B.Order.Infrastructure.Messaging;
using B2B.Order.Infrastructure.Persistence;
using B2B.Order.Infrastructure.Persistence.Repositories;
using B2B.Order.Infrastructure.Services;
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
}
