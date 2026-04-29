using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using B2B.Vendor.Application.Commands.RegisterVendor;
using B2B.Vendor.Infrastructure.Extensions;
using B2B.Vendor.Infrastructure.Persistence;
using B2B.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Vendor"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var assemblies = new[]
{
    Assembly.GetExecutingAssembly(),
    typeof(RegisterVendorCommand).Assembly,
    typeof(B2B.Vendor.Domain.Entities.Vendor).Assembly
};

builder.Services
    .AddSharedInfrastructure(builder.Configuration, "B2B.Vendor", assemblies)
    .AddVendorInfrastructure(builder.Configuration);

builder.Services.AddEventBus(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseSharedMiddleware();
app.MapControllers();

await app.RunAsync();
