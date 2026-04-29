using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Vendor.Application.Interfaces;
using B2B.Vendor.Infrastructure.Persistence;
using B2B.Vendor.Infrastructure.Persistence.Repositories;
using B2B.Shared.Infrastructure.Extensions;

namespace B2B.Vendor.Infrastructure.Extensions;

public static class VendorInfrastructureExtensions
{
    public static IServiceCollection AddVendorInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddPostgresWithReadReplica<VendorDbContext>(config);

        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddSingleton<IReadVendorRepository, VendorReadRepository>();
        services.AddScoped<B2B.Shared.Core.Interfaces.IUnitOfWork>(sp => sp.GetRequiredService<VendorDbContext>());

        return services;
    }
}
