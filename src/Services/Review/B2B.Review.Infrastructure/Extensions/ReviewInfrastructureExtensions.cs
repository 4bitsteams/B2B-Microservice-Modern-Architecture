using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Review.Application.Interfaces;
using B2B.Review.Infrastructure.Persistence;
using B2B.Review.Infrastructure.Persistence.Repositories;
using B2B.Shared.Infrastructure.Extensions;

namespace B2B.Review.Infrastructure.Extensions;

public static class ReviewInfrastructureExtensions
{
    public static IServiceCollection AddReviewInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddPostgresWithReadReplica<ReviewDbContext>(config);

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddSingleton<IReadReviewRepository, ReviewReadRepository>();
        services.AddScoped<B2B.Shared.Core.Interfaces.IUnitOfWork>(sp => sp.GetRequiredService<ReviewDbContext>());

        return services;
    }
}
