using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Discount.Application.Interfaces;
using B2B.Discount.Infrastructure.Persistence;
using B2B.Discount.Infrastructure.Persistence.Repositories;
using B2B.Shared.Infrastructure.Extensions;

namespace B2B.Discount.Infrastructure.Extensions;

public static class DiscountInfrastructureExtensions
{
    public static IServiceCollection AddDiscountInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddPostgresWithReadReplica<DiscountDbContext>(config);

        services.AddScoped<IDiscountRepository, DiscountRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddSingleton<IReadDiscountRepository, DiscountReadRepository>();
        services.AddSingleton<IReadCouponRepository, CouponReadRepository>();
        services.AddScoped<B2B.Shared.Core.Interfaces.IUnitOfWork>(sp => sp.GetRequiredService<DiscountDbContext>());

        return services;
    }
}
