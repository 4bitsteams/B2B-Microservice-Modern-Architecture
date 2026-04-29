using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Shipping.Application.Interfaces;
using B2B.Shipping.Infrastructure.Persistence;
using B2B.Shipping.Infrastructure.Persistence.Repositories;
using B2B.Shared.Infrastructure.Extensions;

namespace B2B.Shipping.Infrastructure.Extensions;

public static class ShippingInfrastructureExtensions
{
    public static IServiceCollection AddShippingInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddPostgresWithReadReplica<ShipmentDbContext>(config);

        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddSingleton<IReadShipmentRepository, ShipmentReadRepository>();
        services.AddScoped<B2B.Shared.Core.Interfaces.IUnitOfWork>(sp => sp.GetRequiredService<ShipmentDbContext>());

        return services;
    }
}
