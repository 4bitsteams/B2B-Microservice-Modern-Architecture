using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using B2B.Product.Application.Commands.CreateProduct;
using B2B.Product.Infrastructure;
using B2B.Product.Infrastructure.Messaging;
using B2B.Product.Infrastructure.Persistence;
using B2B.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Product"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var assemblies = new[]
{
    Assembly.GetExecutingAssembly(),
    typeof(CreateProductCommand).Assembly,
    typeof(B2B.Product.Domain.Entities.Product).Assembly
};

builder.Services
    .AddSharedInfrastructure(builder.Configuration, "B2B.Product", assemblies)
    .AddProductInfrastructure(builder.Configuration);

// Register MassTransit with stock reservation consumers (respond to Order saga)
builder.Services.AddEventBus(builder.Configuration, x =>
{
    x.AddConsumer<StockReservationConsumer>();
    x.AddConsumer<ReleaseStockConsumer>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseSharedMiddleware();   // includes UseCorrelationId()
app.MapControllers();

await app.RunAsync();
