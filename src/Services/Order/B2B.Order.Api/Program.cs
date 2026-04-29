using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using B2B.Order.Application.Commands.CreateOrder;
using B2B.Order.Application.Sagas;
using B2B.Order.Infrastructure;
using B2B.Order.Infrastructure.Messaging;
using B2B.Order.Infrastructure.Persistence;
using B2B.Product.Infrastructure.Messaging;
using B2B.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Order"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var assemblies = new[]
{
    Assembly.GetExecutingAssembly(),
    typeof(CreateOrderCommand).Assembly,
    typeof(B2B.Order.Domain.Entities.Order).Assembly
};

builder.Services
    .AddSharedInfrastructure(builder.Configuration, "B2B.Order", assemblies)
    .AddOrderInfrastructure(builder.Configuration);

// ── MassTransit: OrderFulfillmentSaga + all saga participant consumers ─────────
//
// Architecture
// ────────────
//   • OrderFulfillmentSaga       — EF Core-persisted state machine (saga orchestrator)
//   • StockReservationConsumer   — Product service consumer (existing, in Product.Infrastructure)
//   • ReleaseStockConsumer       — Product service consumer (existing, in Product.Infrastructure)
//   • PaymentConsumer            — Stub payment consumer (replace with B2B.Payment microservice)
//   • RefundPaymentConsumer      — Stub refund consumer  (replace with B2B.Payment microservice)
//   • ShipmentConsumer           — Stub shipment consumer (replace with B2B.Shipping microservice)
//
// Timeout scheduling
// ──────────────────
//   UseDelayedMessageScheduler() uses the RabbitMQ delayed message exchange plugin.
//   Enable the plugin in RabbitMQ:
//     rabbitmq-plugins enable rabbitmq_delayed_message_exchange
//   Or use the pre-built Docker image (see docker-compose.yml).
//
//   For production with higher reliability, replace with Quartz.NET:
//     x.AddQuartzConsumers();
//     cfg.UseMessageScheduler(new Uri("queue:quartz"));
builder.Services.AddEventBus(builder.Configuration, x =>
{
    // ── Saga (orchestrator) ────────────────────────────────────────────────────
    x.AddSagaStateMachine<OrderFulfillmentSaga, OrderFulfillmentSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<OrderDbContext>();
        });

    // ── Stock participants (Product service logic — kept here for single-repo dev) ──
    x.AddConsumer<StockReservationConsumer>();
    x.AddConsumer<ReleaseStockConsumer>();

    // ── Payment participants (stub — replace with B2B.Payment service) ─────────
    x.AddConsumer<PaymentConsumer>();
    x.AddConsumer<RefundPaymentConsumer>();

    // ── Shipment participants (stub — replace with B2B.Shipping service) ───────
    x.AddConsumer<ShipmentConsumer>();

    // ── Basket checkout → Order creation ──────────────────────────────────────
    x.AddConsumer<BasketCheckedOutConsumer>();
},
configureRabbitMq: cfg =>
{
    // Enables saga timeout scheduling via the RabbitMQ delayed message exchange.
    // Requires rabbitmq_delayed_message_exchange plugin on the broker.
    cfg.UseDelayedMessageScheduler();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseSharedMiddleware();   // includes UseCorrelationId()
app.MapControllers();

await app.RunAsync();
