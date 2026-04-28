using System.Reflection;
using System.Text;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.Behaviors;
using B2B.Shared.Infrastructure.Caching;
using B2B.Shared.Infrastructure.Http;
using B2B.Shared.Infrastructure.Locking;
using B2B.Shared.Infrastructure.Messaging;
using B2B.Shared.Infrastructure.Notifications;
using B2B.Shared.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace B2B.Shared.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        Assembly[] assemblies)
    {
        services
            .AddMediatRWithBehaviors(assemblies, config)
            .AddValidators(assemblies)
            .AddRedisCache(config)
            .AddJwtAuthentication(config)
            .AddCurrentUser()
            .AddCorrelationId()
            .AddOpenTelemetry(config, serviceName)
            .AddDefaultHealthChecks(config)
            .AddDistributedLock()
            .AddPlatformServices(config)
            .AddNotifications();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="ICorrelationIdProvider"/> service.
    /// Call <see cref="UseCorrelationId"/> on the <see cref="WebApplication"/> to
    /// activate the middleware that reads/generates the <c>X-Correlation-ID</c> header.
    /// </summary>
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        return services;
    }

    public static IServiceCollection AddMediatRWithBehaviors(
        this IServiceCollection services, Assembly[] assemblies, IConfiguration? config = null)
    {
        // Bind RetryBehavior options from configuration (defaults apply when section is absent).
        if (config is not null)
            services.Configure<RetryBehaviorOptions>(config.GetSection(RetryBehaviorOptions.SectionName));
        else
            services.Configure<RetryBehaviorOptions>(_ => { }); // use class defaults

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);

            // Pipeline order (outermost → innermost → handler):
            //
            //   LoggingBehavior        — structured request/response logs
            //   RetryBehavior          — transient fault retry (commands only)
            //   IdempotencyBehavior    — deduplication via idempotency key (commands only)
            //   PerformanceBehavior    — slow query/command detection
            //   AuthorizationBehavior  — resource-based authorization (commands + queries)
            //   ValidationBehavior     — FluentValidation (short-circuits on error)
            //   AuditBehavior          — compliance audit trail (commands only)
            //   DomainEventBehavior    — publishes domain events after SaveChangesAsync (innermost)
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(RetryBehavior<,>));
            cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuditBehavior<,>));
            cfg.AddOpenBehavior(typeof(DomainEventBehavior<,>));
        });

        return services;
    }

    /// <summary>
    /// Registers the Redis-backed distributed lock service.
    /// Requires <see cref="IConnectionMultiplexer"/> (registered by <see cref="AddRedisCache"/>).
    /// </summary>
    public static IServiceCollection AddDistributedLock(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();
        return services;
    }

    /// <summary>
    /// Registers pluggable platform services: tax calculation, tiered pricing, and audit trail.
    /// Each can be replaced by a custom implementation without touching application code.
    /// </summary>
    public static IServiceCollection AddPlatformServices(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<ITaxService, PercentageTaxService>();
        services.AddSingleton<IPricingService, TieredPricingService>();
        services.AddSingleton<IAuditService, SerilogAuditService>();
        return services;
    }

    /// <summary>
    /// Registers the composite notification service.
    /// Add channel handlers individually per-service as needed:
    /// <code>services.AddScoped&lt;INotificationChannelHandler, EmailNotificationHandler&gt;();</code>
    /// </summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, CompositeNotificationService>();
        return services;
    }

    public static IServiceCollection AddValidators(
        this IServiceCollection services, Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }

    public static IServiceCollection AddRedisCache(
        this IServiceCollection services, IConfiguration config)
    {
        var redisConnection = config.GetConnectionString("Redis") ?? "localhost:6379";

        // Register IConnectionMultiplexer as singleton for RemoveByPrefixAsync (SCAN + DEL)
        // and for any distributed-lock use cases.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        services.AddStackExchangeRedisCache(opt => opt.Configuration = redisConnection);
        services.AddSingleton<ICacheService, RedisCacheService>();
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        var jwtSettings = config.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        return services;
    }

    /// <summary>
    /// Registers MassTransit with RabbitMQ, the in-memory outbox, and standard retry intervals.
    /// </summary>
    /// <param name="configureConsumers">
    ///   Register consumers, sagas, and other bus participants
    ///   (e.g. <c>x.AddConsumer&lt;MyConsumer&gt;()</c>).
    /// </param>
    /// <param name="configureRabbitMq">
    ///   Optional additional RabbitMQ transport configuration applied after the
    ///   default host/retry/outbox setup — use this to add service-specific
    ///   middleware such as <c>cfg.UseDelayedMessageScheduler()</c> for saga timeouts.
    /// </param>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services, IConfiguration config,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<MassTransit.IRabbitMqBusFactoryConfigurator>? configureRabbitMq = null)
    {
        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var rabbitMq = config.GetSection("RabbitMQ");
                cfg.Host(rabbitMq["Host"] ?? "localhost", rabbitMq["VirtualHost"] ?? "/", h =>
                {
                    h.Username(rabbitMq["Username"] ?? "guest");
                    h.Password(rabbitMq["Password"] ?? "guest");
                });

                cfg.UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000, 5000));
                cfg.UseInMemoryOutbox(ctx);

                // Apply caller-supplied transport configuration (e.g. delayed scheduler)
                configureRabbitMq?.Invoke(cfg);

                cfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddScoped<IEventBus, MassTransitEventBus>();
        return services;
    }

    public static IServiceCollection AddOpenTelemetry(
        this IServiceCollection services, IConfiguration config, string serviceName)
    {
        var otlpEndpoint = config["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }

    public static IServiceCollection AddDefaultHealthChecks(
        this IServiceCollection services, IConfiguration config)
    {
        var hcBuilder = services.AddHealthChecks();

        var pg = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(pg))
            hcBuilder.AddNpgSql(pg, tags: ["db"]);

        var redis = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redis))
            hcBuilder.AddRedis(redis, tags: ["cache"]);

        return services;
    }

    public static WebApplication UseSharedMiddleware(this WebApplication app)
    {
        // CorrelationId must run first so all subsequent middleware and MediatR
        // pipeline behaviors have access to the ID via ICorrelationIdProvider.
        app.UseCorrelationId();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
        });
        return app;
    }

    /// <summary>
    /// Adds <see cref="CorrelationIdMiddleware"/> to the request pipeline.
    /// Reads <c>X-Correlation-ID</c> from the request (or generates one) and
    /// echoes it in the response.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();

    public static IServiceCollection AddPostgres<TContext>(
        this IServiceCollection services, IConfiguration config)
        where TContext : DbContext
    {
        var connStr = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' not found.");

        services.AddDbContext<TContext>(opt =>
            opt.UseNpgsql(connStr, npg =>
            {
                npg.EnableRetryOnFailure(3);
                npg.CommandTimeout(30);
                npg.MinBatchSize(1);
            })
            .EnableDetailedErrors(false)
            .EnableSensitiveDataLogging(false));

        // Register the base DbContext type so DomainEventBehavior (which injects
        // DbContext) can be resolved without knowing the concrete context type.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());

        return services;
    }

    /// <summary>
    /// Registers two database connections for a service that has a read replica:
    ///
    ///   • Scoped <typeparamref name="TContext"/> → primary (write) connection, change tracking ON.
    ///     Used by command handlers / repositories via IUnitOfWork.
    ///
    ///   • Singleton <see cref="IDbContextFactory{TContext}"/> → read-replica connection,
    ///     QueryTrackingBehavior.NoTracking globally.
    ///     Used by read repositories injected into query handlers.
    ///
    /// If <c>ReadReplicaConnection</c> is not configured, the factory falls back to
    /// <c>DefaultConnection</c> so single-node dev environments work without changes.
    ///
    /// Registration order matters:
    ///   1. AddDbContextFactory registers IDbContextFactory (singleton, replica) AND TContext (scoped from factory).
    ///   2. AddDbContext then overwrites only the scoped TContext to point to the primary.
    ///   The factory singleton is not touched by step 2.
    /// </summary>
    public static IServiceCollection AddPostgresWithReadReplica<TContext>(
        this IServiceCollection services, IConfiguration config)
        where TContext : DbContext
    {
        var writeConnStr = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' not found.");

        // Fall back to primary if no read replica is configured (single-node dev / test).
        var readConnStr = config.GetConnectionString("ReadReplicaConnection") ?? writeConnStr;

        // ── Step 1: factory for READ path (replica, NoTracking) ──────────────────
        // Registers IDbContextFactory<TContext> as singleton + TContext as scoped (from factory).
        services.AddDbContextFactory<TContext>(opt =>
            opt.UseNpgsql(readConnStr, npg =>
            {
                npg.EnableRetryOnFailure(3);
                npg.CommandTimeout(30);
                npg.MinBatchSize(1);
            })
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .EnableDetailedErrors(false)
            .EnableSensitiveDataLogging(false),
            ServiceLifetime.Singleton);

        // ── Step 2: scoped context for WRITE path (primary, tracking) ────────────
        // Overwrites the scoped TContext that step 1 registered, so DI resolves TContext
        // to the primary connection for commands. The IDbContextFactory<TContext> singleton
        // is unaffected and continues to produce replica contexts.
        services.AddDbContext<TContext>(opt =>
            opt.UseNpgsql(writeConnStr, npg =>
            {
                npg.EnableRetryOnFailure(3);
                npg.CommandTimeout(30);
                npg.MinBatchSize(1);
            })
            .EnableDetailedErrors(false)
            .EnableSensitiveDataLogging(false));

        // Register the base DbContext type so DomainEventBehavior can be resolved.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());

        return services;
    }
}
