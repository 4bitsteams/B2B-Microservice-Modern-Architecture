using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using B2B.Payment.Application.Commands.ProcessPayment;
using B2B.Payment.Infrastructure.Extensions;
using B2B.Payment.Infrastructure.Persistence;
using B2B.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Payment"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var assemblies = new[]
{
    Assembly.GetExecutingAssembly(),
    typeof(ProcessPaymentCommand).Assembly,
    typeof(B2B.Payment.Domain.Entities.Payment).Assembly
};

builder.Services
    .AddSharedInfrastructure(builder.Configuration, "B2B.Payment", assemblies)
    .AddPaymentInfrastructure(builder.Configuration);

builder.Services.AddEventBus(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseSharedMiddleware();
app.MapControllers();

await app.RunAsync();
