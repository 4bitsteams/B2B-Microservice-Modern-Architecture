using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using B2B.Order.Application.Commands.CreateOrder;
using B2B.Order.Infrastructure;
using B2B.Order.Infrastructure.Persistence;
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

// ── MassTransit: saga + consumers registered via Infrastructure façade ────────
//
// DIP — Program.cs never imports Product.Infrastructure, saga framework types,
// or individual consumer classes.  All concrete registrations live in
// DependencyInjection.ConfigureOrderBusParticipants (Infrastructure layer).
//
// Timeout scheduling uses the RabbitMQ delayed message exchange plugin.
// For higher reliability in production, replace with Quartz.NET:
//   x.AddQuartzConsumers();  cfg.UseMessageScheduler(new Uri("queue:quartz"));
builder.Services.AddEventBus(
    builder.Configuration,
    configureConsumers: DependencyInjection.ConfigureOrderBusParticipants,
    configureRabbitMq:  cfg => cfg.UseDelayedMessageScheduler());

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
