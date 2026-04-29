using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using B2B.Shipping.Application.Commands.CreateShipment;
using B2B.Shipping.Infrastructure.Extensions;
using B2B.Shipping.Infrastructure.Persistence;
using B2B.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Shipping"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var assemblies = new[]
{
    Assembly.GetExecutingAssembly(),
    typeof(CreateShipmentCommand).Assembly,
    typeof(B2B.Shipping.Domain.Entities.Shipment).Assembly
};

builder.Services
    .AddSharedInfrastructure(builder.Configuration, "B2B.Shipping", assemblies)
    .AddShippingInfrastructure(builder.Configuration);

builder.Services.AddEventBus(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseSharedMiddleware();
app.MapControllers();

await app.RunAsync();
