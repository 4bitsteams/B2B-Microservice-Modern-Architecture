using System.Reflection;
using Scalar.AspNetCore;
using Serilog;
using B2B.Basket.Application.Commands.AddToBasket;
using B2B.Basket.Infrastructure.Extensions;
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

builder.Services.AddEventBus(builder.Configuration);

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
