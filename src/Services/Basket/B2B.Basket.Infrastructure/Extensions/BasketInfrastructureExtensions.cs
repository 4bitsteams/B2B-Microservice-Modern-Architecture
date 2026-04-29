using B2B.Basket.Application.Interfaces;
using B2B.Basket.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Basket.Infrastructure.Extensions;

public static class BasketInfrastructureExtensions
{
    public static IServiceCollection AddBasketInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBasketRepository, RedisBasketRepository>();
        return services;
    }
}
