using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using B2B.Notification.Worker.Consumers;
using B2B.Notification.Worker.Contracts;
using B2B.Notification.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Notification")
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.AddScoped<IEmailService, SmtpEmailService>();

var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

builder.Services.AddMassTransit(x =>
{
    // Base transport required by MassTransit even when the Rider handles all messaging.
    x.UsingInMemory();

    x.AddRider(rider =>
    {
        // ── Register all notification consumers ───────────────────────────────
        rider.AddConsumer<OrderConfirmedConsumer>();
        rider.AddConsumer<UserRegisteredConsumer>();
        rider.AddConsumer<ProductLowStockConsumer>();
        rider.AddConsumer<OrderProcessingStartedConsumer>();
        rider.AddConsumer<OrderPaymentProcessedConsumer>();
        rider.AddConsumer<OrderFulfilledConsumer>();
        rider.AddConsumer<OrderCancelledDueToStockConsumer>();
        rider.AddConsumer<OrderCancelledDueToPaymentConsumer>();
        rider.AddConsumer<OrderCancelledDueToShipmentConsumer>();

        rider.UsingKafka((ctx, k) =>
        {
            k.Host(kafkaBootstrap);

            // ── Order lifecycle ───────────────────────────────────────────────
            k.TopicEndpoint<OrderConfirmedIntegration>(
                Topics.OrderConfirmed, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<OrderConfirmedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });

            k.TopicEndpoint<OrderProcessingStartedIntegration>(
                Topics.OrderProcessingStarted, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<OrderProcessingStartedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });

            k.TopicEndpoint<OrderPaymentProcessedIntegration>(
                Topics.OrderPaymentProcessed, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<OrderPaymentProcessedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });

            k.TopicEndpoint<OrderFulfilledIntegration>(
                Topics.OrderFulfilled, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<OrderFulfilledConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });

            k.TopicEndpoint<OrderCancelledDueToStockIntegration>(
                Topics.OrderCancelledStock, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<OrderCancelledDueToStockConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });

            k.TopicEndpoint<OrderCancelledDueToPaymentIntegration>(
                Topics.OrderCancelledPayment, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<OrderCancelledDueToPaymentConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });

            k.TopicEndpoint<OrderCancelledDueToShipmentIntegration>(
                Topics.OrderCancelledShipment, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<OrderCancelledDueToShipmentConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });

            // ── Identity ──────────────────────────────────────────────────────
            k.TopicEndpoint<UserRegisteredIntegration>(
                Topics.UserRegistered, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<UserRegisteredConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });

            // ── Product ───────────────────────────────────────────────────────
            k.TopicEndpoint<ProductLowStockIntegration>(
                Topics.ProductLowStock, Topics.ConsumerGroup, e =>
                {
                    e.ConfigureConsumer<ProductLowStockConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(500, 1_000, 5_000));
                });
        });
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("B2B.Notification"))
    .WithTracing(tracing => tracing
        .AddOtlpExporter(o => o.Endpoint = new Uri(
            builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317")));

builder.Services.AddHealthChecks();

var host = builder.Build();
await host.RunAsync();

// ── Topic name constants ──────────────────────────────────────────────────────
// Mirror of B2B.Shared.Core.Messaging.KafkaTopics. The Worker deliberately does
// not reference B2B.Shared.Core to avoid tight coupling. Keep these in sync.
// Type declarations must follow all top-level statements in C# top-level programs.
static class Topics
{
    public const string OrderConfirmed         = "b2b-order-confirmed";
    public const string OrderProcessingStarted = "b2b-order-processing-started";
    public const string OrderPaymentProcessed  = "b2b-order-payment-processed";
    public const string OrderFulfilled         = "b2b-order-fulfilled";
    public const string OrderCancelledStock    = "b2b-order-cancelled-stock";
    public const string OrderCancelledPayment  = "b2b-order-cancelled-payment";
    public const string OrderCancelledShipment = "b2b-order-cancelled-shipment";
    public const string UserRegistered         = "b2b-identity-user-registered";
    public const string ProductLowStock        = "b2b-product-low-stock";

    // Single consumer group — the worker gets exactly one delivery per topic.
    public const string ConsumerGroup = "b2b-notification-worker";
}
