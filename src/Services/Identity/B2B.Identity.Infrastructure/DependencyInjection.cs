using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Infrastructure.Persistence;
using B2B.Identity.Infrastructure.Persistence.Repositories;
using B2B.Identity.Infrastructure.Services;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.Extensions;
using B2B.Shared.Infrastructure.Security;

namespace B2B.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Registers:
        //   IDbContextFactory<IdentityDbContext> (singleton) → read replica, NoTracking
        //   IdentityDbContext                    (scoped)    → primary, tracking
        services.AddPostgresWithReadReplica<IdentityDbContext>(config);

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());

        // Write repositories (primary)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();

        // Read repositories (replica) — singleton lifetime mirrors the factory
        services.AddSingleton<IReadUserRepository, UserReadRepository>();
        services.AddSingleton<IReadTenantRepository, TenantReadRepository>();
        services.AddSingleton<IReadRoleRepository, RoleReadRepository>();

        // Singleton: constructor caches SigningCredentials and TokenValidationParameters;
        // JsonWebTokenHandler is thread-safe. Avoids per-request crypto object allocation.
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        return services;
    }
}
