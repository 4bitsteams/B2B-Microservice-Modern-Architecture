using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Product.Application.Interfaces;
using B2B.Product.Infrastructure.Persistence;
using B2B.Product.Infrastructure.Persistence.Repositories;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.Extensions;

namespace B2B.Product.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Registers:
        //   IDbContextFactory<ProductDbContext> (singleton) → read replica, NoTracking
        //   ProductDbContext             (scoped)           → primary, tracking
        services.AddPostgresWithReadReplica<ProductDbContext>(config);

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProductDbContext>());

        // Write repository (primary)
        services.AddScoped<IProductRepository, ProductRepository>();

        // Read repository (replica) — singleton lifetime mirrors the factory
        services.AddSingleton<IReadProductRepository, ProductReadRepository>();

        // Write repository (primary)
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        // Read repository (replica) — singleton lifetime mirrors the factory
        services.AddSingleton<IReadCategoryRepository, CategoryReadRepository>();

        return services;
    }
}
