using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using B2B.Notification.Worker.Consumers;
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

builder.Services.AddMassTransit(x =>
{
    // Notification consumers — triggered by domain events from Order service
    x.AddConsumer<OrderConfirmedConsumer>();
    x.AddConsumer<UserRegisteredConsumer>();
    x.AddConsumer<ProductLowStockConsumer>();

    // Saga outcome consumers — triggered by OrderFulfillmentSaga state transitions
    x.AddConsumer<OrderProcessingStartedConsumer>();
    x.AddConsumer<OrderPaymentProcessedConsumer>();
    x.AddConsumer<OrderFulfilledConsumer>();
    x.AddConsumer<OrderCancelledDueToStockConsumer>();
    x.AddConsumer<OrderCancelledDueToPaymentConsumer>();
    x.AddConsumer<OrderCancelledDueToShipmentConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var rabbitMq = builder.Configuration.GetSection("RabbitMQ");
        cfg.Host(rabbitMq["Host"] ?? "localhost", rabbitMq["VirtualHost"] ?? "/", h =>
        {
            h.Username(rabbitMq["Username"] ?? "guest");
            h.Password(rabbitMq["Password"] ?? "guest");
        });

        cfg.UseMessageRetry(r => r.Intervals(500, 1000, 5000));
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("B2B.Notification"))
    .WithTracing(tracing => tracing
        .AddOtlpExporter(o => o.Endpoint = new Uri(
            builder.Configuration["OpenTelemetry:Endpoint"]!)));

builder.Services.AddHealthChecks();

var host = builder.Build();
await host.RunAsync();
