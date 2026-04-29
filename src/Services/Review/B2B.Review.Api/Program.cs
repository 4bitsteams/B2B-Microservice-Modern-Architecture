using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using B2B.Review.Application.Commands.SubmitReview;
using B2B.Review.Infrastructure.Extensions;
using B2B.Review.Infrastructure.Persistence;
using B2B.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Review"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var assemblies = new[]
{
    Assembly.GetExecutingAssembly(),
    typeof(SubmitReviewCommand).Assembly,
    typeof(B2B.Review.Domain.Entities.Review).Assembly
};

builder.Services
    .AddSharedInfrastructure(builder.Configuration, "B2B.Review", assemblies)
    .AddReviewInfrastructure(builder.Configuration);

builder.Services.AddEventBus(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseSharedMiddleware();
app.MapControllers();

await app.RunAsync();
