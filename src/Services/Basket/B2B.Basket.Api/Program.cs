using System.Reflection;
using MassTransit;
using Scalar.AspNetCore;
using Serilog;
using B2B.Basket.Application.Commands.AddToBasket;
using B2B.Basket.Infrastructure.Extensions;
using B2B.Shared.Core.Messaging;
using B2B.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Basket"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var assemblies = new[]
{
    Assembly.GetExecutingAssembly(),
    typeof(AddToBasketCommand).Assembly,
    typeof(B2B.Basket.Domain.Entities.Basket).Assembly
};

builder.Services
    .AddSharedInfrastructure(builder.Configuration, "B2B.Basket", assemblies)
    .AddBasketInfrastructure();

// Register Kafka producer so IEventBus.PublishAsync<BasketCheckedOutIntegration>
// delivers to the Kafka topic consumed by the Order service.
var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
builder.Services.AddEventBus(builder.Configuration, configureRider: rider =>
{
    rider.AddProducer<BasketCheckedOutIntegration>(KafkaTopics.BasketCheckedOut);
    rider.UsingKafka((_, k) => k.Host(kafkaBootstrap));
});

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSharedMiddleware();
app.MapControllers();

await app.RunAsync();
