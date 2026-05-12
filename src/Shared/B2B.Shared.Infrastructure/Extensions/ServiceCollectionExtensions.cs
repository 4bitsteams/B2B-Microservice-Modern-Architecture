using System.IO.Compression;
using System.Reflection;
using System.Text;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.BackgroundServices;
using B2B.Shared.Infrastructure.Behaviors;
using B2B.Shared.Infrastructure.Caching;
using B2B.Shared.Infrastructure.Channels;
using B2B.Shared.Infrastructure.Http;
using B2B.Shared.Infrastructure.Locking;
using B2B.Shared.Infrastructure.Messaging;
using B2B.Shared.Infrastructure.Middleware;
using B2B.Shared.Infrastructure.Notifications;
using B2B.Shared.Infrastructure.Observability;
using B2B.Shared.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace B2B.Shared.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    // ── Infrastructure constants ───────────────────────────────────────────────

    /// <summary>
    /// EF Core command timeout in seconds. Raised above the default (30s) to
    /// accommodate slow migrations and reporting queries; keep below HTTP gateway
    /// timeout so callers always get a response rather than a silent hang.
    /// </summary>
    private const int DbCommandTimeoutSeconds = 30;

    /// <summary>
    /// Retry intervals (ms) for the MassTransit in-process retry policy.
    /// Exponential-ish progression: 100 ms → 500 ms → 1 s → 2 s → 5 s.
    /// Covers transient broker disconnects without flooding a recovering broker.
    /// </summary>
    private static readonly int[] MessageBusRetryIntervals = [100, 500, 1_000, 2_000, 5_000];

    /// <summary>Default Redis connection used when no connection string is configured (local dev only).</summary>
    private const string DefaultRedisConnection = "localhost:6379";

    /// <summary>Default OTLP collector endpoint used when not configured (local dev only).</summary>
    private const string DefaultOtlpEndpoint = "http://localhost:4317";

    // ── HybridCache constants ──────────────────────────────────────────────────

    /// <summary>Maximum serialized payload size accepted by HybridCache (L1+L2). 1 MB.</summary>
    private const long HybridCacheMaxPayloadBytes = 1024 * 1024;

    /// <summary>L1 (in-process) cache TTL — short to bound memory growth per replica.</summary>
    private static readonly TimeSpan HybridCacheL1Expiry = TimeSpan.FromMinutes(2);

    /// <summary>L2 (Redis) cache TTL — matches the existing RedisCacheService default.</summary>
    private static readonly TimeSpan HybridCacheL2Expiry = TimeSpan.FromMinutes(15);


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
            .AddServiceHybridCache()
            .AddResponseCompression(opt =>
            {
                // Brotli first (better ratio); gzip as fallback for older clients.
                // EnableForHttps: safe here because we control all TLS termination at
                // the YARP Gateway — internal traffic can compress freely.
                opt.EnableForHttps = true;
                opt.Providers.Add<BrotliCompressionProvider>();
                opt.Providers.Add<GzipCompressionProvider>();
                opt.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                [
                    "application/json",
                    "application/problem+json"
                ]);
            })
            .AddOutputCache(opt =>
            {
                // Base policy: short TTL for safety; endpoints opt-in via [OutputCache].
                opt.AddBasePolicy(policy => policy.Expire(TimeSpan.FromSeconds(10)));

                // "queries" policy: used by GET endpoints returning stable read-model data.
                opt.AddPolicy("queries", policy => policy
                    .Expire(TimeSpan.FromSeconds(30))
                    .SetVaryByQuery("page", "pageSize", "tenantId")
                    .Tag("queries"));
            })
            .AddJwtAuthentication(config)
            .AddCurrentUser()
            .AddCorrelationId()
            .AddServiceOpenTelemetry(config, serviceName)
            .AddDefaultHealthChecks(config)
            .AddDistributedLock()
            .AddPlatformServices(config)
            .AddNotifications()
            .AddObjectPools();

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

        // Singleton bulkhead provider — one SemaphoreSlim per command type, shared
        // across all ResilienceBehavior instances for the same TRequest.
        services.AddSingleton<CommandBulkheadProvider>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);

            // Pipeline order (outermost → innermost → handler):
            //
            //   LoggingBehavior        — structured request/response logs; marks OTel spans on failure
            //   ErrorMetricsBehavior   — records business-error counters (APM/Grafana) after retries
            //   RetryBehavior          — transient fault retry (commands only)
            //   IdempotencyBehavior    — deduplication via idempotency key (commands only)
            //   PerformanceBehavior    — slow query/command detection
            //   AuthorizationBehavior  — resource-based authorization (commands + queries)
            //   ValidationBehavior     — FluentValidation (short-circuits on error)
            //   AuditBehavior          — compliance audit trail (commands only)
            //   DomainEventBehavior    — publishes domain events after SaveChangesAsync (innermost)
            //
            // ErrorMetricsBehavior sits OUTSIDE RetryBehavior so it counts one metric
            // per user request, not per retry attempt.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ErrorMetricsBehavior<,>));
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
    /// Registers pluggable platform services: tax calculation, tiered pricing, audit trail,
    /// and error metrics (APM/Grafana).
    /// Each can be replaced by a custom implementation without touching application code.
    /// </summary>
    public static IServiceCollection AddPlatformServices(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<ITaxService, PercentageTaxService>();
        services.AddSingleton<IPricingService, TieredPricingService>();
        services.AddSingleton<IAuditService, SerilogAuditService>();

        // IErrorMetricsService — singleton because Meter instruments are thread-safe
        // and should be shared across all DI scopes (one counter per process, not per request).
        services.AddSingleton<IErrorMetricsService, ErrorMetricsService>();

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
        var redisConnection = config.GetConnectionString("Redis") ?? DefaultRedisConnection;

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
    /// Registers MassTransit with an in-memory base bus for sagas and co-located consumers,
    /// plus an optional Apache Kafka Rider for cross-service integration event delivery.
    /// </summary>
    /// <param name="configureConsumers">
    ///   Register sagas, co-located consumers, and other in-memory bus participants
    ///   (e.g. <c>x.AddSagaStateMachine&lt;…&gt;()</c>, <c>x.AddConsumer&lt;…&gt;()</c>).
    ///   Saga timeout scheduling is handled automatically via <c>UseInMemoryScheduler</c>.
    /// </param>
    /// <param name="configureRider">
    ///   Optional Kafka Rider configuration for cross-service messaging.
    ///   Register producers with <c>rider.AddProducer&lt;T&gt;(topic)</c> and consumers
    ///   with <c>rider.AddConsumer&lt;T&gt;()</c>, then call <c>rider.UsingKafka(…)</c>
    ///   to configure the host and topic endpoints. When omitted, <see cref="IEventBus"/>
    ///   falls back to in-memory <c>IBus.Publish</c> (same-process only).
    /// </param>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services, IConfiguration config,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IRiderRegistrationConfigurator>? configureRider = null)
    {
        services.AddMassTransit(x =>
        {
            // Register sagas, co-located stubs, and in-process consumers.
            configureConsumers?.Invoke(x);

            // In-memory transport — handles saga state machine scheduling and
            // co-located consumer communication without any external broker dependency.
            // The in-memory bus supports scheduled/delayed messages natively, replacing
            // the RabbitMQ delayed message exchange plugin for saga timeouts.
            // For production multi-node deployments, switch to Quartz.NET:
            //   x.AddQuartzConsumers(); cfg.UseMessageScheduler(new Uri("queue:quartz"));
            x.UsingInMemory((ctx, cfg) =>
            {
                cfg.ConfigureEndpoints(ctx);
            });

            // Kafka Rider — cross-service integration events (optional per service).
            // Services that only do in-process communication can omit configureRider.
            if (configureRider is not null)
            {
                x.AddRider(rider => configureRider(rider));
            }
        });

        services.AddScoped<IEventBus, MassTransitEventBus>();
        return services;
    }

    /// <summary>
    /// Registers HybridCache (L1 in-process + L2 Redis) for use alongside
    /// <see cref="ICacheService"/>. Inject <c>HybridCache</c> directly in query
    /// handlers for automatic stampede protection, local memory caching, and
    /// Redis L2 fallback — all without manual SemaphoreSlim wiring.
    /// </summary>
    public static IServiceCollection AddServiceHybridCache(this IServiceCollection services)
    {
        services.AddHybridCache(opt =>
        {
            opt.MaximumPayloadBytes = HybridCacheMaxPayloadBytes;
            opt.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = HybridCacheL2Expiry,
                LocalCacheExpiration = HybridCacheL1Expiry
            };
        });
        return services;
    }

    /// <summary>
    /// Registers OpenTelemetry traces <b>and</b> metrics exported via OTLP.
    ///
    /// Traces: ASP.NET Core + HttpClient + EF Core → Jaeger / OTLP collector.
    /// Metrics: ASP.NET Core + HttpClient + .NET Runtime + B2B.Platform → OTLP collector
    ///          (collector scrapes and forwards to Prometheus → Grafana).
    ///
    /// The "B2B.Platform" meter (<see cref="ErrorMetricsService.MeterName"/>) captures
    /// business error counters and unhandled exception counters emitted by
    /// <c>ErrorMetricsBehavior</c> and <c>GlobalExceptionMiddleware</c>.
    /// </summary>
    public static IServiceCollection AddServiceOpenTelemetry(
        this IServiceCollection services, IConfiguration config, string serviceName)
    {
        var otlpEndpoint = config["OpenTelemetry:Endpoint"] ?? DefaultOtlpEndpoint;

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                // Register the custom B2B error/exception meter so the OTel SDK
                // picks up all instruments created by ErrorMetricsService.
                .AddMeter(ErrorMetricsService.MeterName)
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return services;
    }

    /// <summary>
    /// Registers health checks for PostgreSQL and Redis.
    /// Each check is tagged so readiness/liveness probes can filter independently.
    /// Kafka connectivity is validated at startup by MassTransit's Rider host;
    /// a custom <c>IHealthCheck</c> implementation can be added per service if needed.
    /// </summary>
    public static IServiceCollection AddDefaultHealthChecks(
        this IServiceCollection services, IConfiguration config)
    {
        var hcBuilder = services.AddHealthChecks();

        var pg = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(pg))
            hcBuilder.AddNpgSql(pg, tags: ["db", "ready"]);

        var redis = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redis))
            hcBuilder.AddRedis(redis, tags: ["cache", "ready"]);

        return services;
    }

    public static WebApplication UseSharedMiddleware(this WebApplication app)
    {
        // CorrelationId first — stamps X-Correlation-ID before any other middleware
        // reads logs/traces, so the ID propagates through the entire request chain.
        app.UseCorrelationId();

        // Global exception handler sits AFTER correlation ID so every error log and
        // ProblemDetails response already carries the correlation ID.
        app.UseGlobalExceptionHandler();

        // Response compression — must run before any middleware that writes the body.
        app.UseResponseCompression();

        app.UseAuthentication();
        app.UseAuthorization();

        // Output cache — sits after auth so cached responses respect authorization.
        app.UseOutputCache();

        // Liveness: is the process up?  Readiness: are backing services reachable?
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,   // no checks — just confirms the process is alive
            ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = hc => hc.Tags.Contains("ready"),
            ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
        });
        // Combined legacy endpoint for backwards compatibility with existing probes
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

    /// <summary>
    /// Adds <see cref="GlobalExceptionMiddleware"/> to the request pipeline.
    ///
    /// Must be registered AFTER <see cref="UseCorrelationId"/> so the correlation
    /// ID is already stamped in <c>HttpContext.Items</c> when an exception is caught.
    /// Any unhandled exception is:
    ///   1. Logged as a structured error (Serilog → Seq).
    ///   2. Recorded as an <c>unhandled_exceptions</c> metric (OTel → Prometheus → Grafana).
    ///   3. Returned to the caller as a <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> JSON body
    ///      containing only the correlation ID and trace ID — no stack trace.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionMiddleware>();

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
                npg.CommandTimeout(DbCommandTimeoutSeconds);
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
                npg.CommandTimeout(DbCommandTimeoutSeconds);
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
                npg.CommandTimeout(DbCommandTimeoutSeconds);
                npg.MinBatchSize(1);
            })
            .EnableDetailedErrors(false)
            .EnableSensitiveDataLogging(false));

        // Register the base DbContext type so DomainEventBehavior can be resolved.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());

        return services;
    }

    // ── Background Consumer Services ─────────────────────────────────────────

    /// <summary>
    /// Registers a <see cref="ConsumerBackgroundService{TMessage}"/> implementation
    /// as an <see cref="Microsoft.Extensions.Hosting.IHostedService"/> together with
    /// its <see cref="ConsumerBackgroundServiceOptions"/>.
    ///
    /// Usage:
    /// <code>
    /// // Register any concrete subclass of ConsumerBackgroundService&lt;T&gt;:
    /// services.AddConsumerBackgroundService&lt;MyConsumer, MyMessage&gt;(
    ///     config, "BackgroundServices:MyConsumer");
    /// </code>
    /// </summary>
    /// <typeparam name="TService">
    /// The concrete <see cref="ConsumerBackgroundService{TMessage}"/> subclass to register.
    /// </typeparam>
    /// <typeparam name="TMessage">
    /// The message type consumed by <typeparamref name="TService"/>.
    /// </typeparam>
    /// <param name="configSection">
    /// The configuration section path that binds to
    /// <see cref="ConsumerBackgroundServiceOptions"/> (e.g. "BackgroundServices:OutboxRelay").
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    public static IServiceCollection AddConsumerBackgroundService<TService, TMessage>(
        this IServiceCollection services,
        IConfiguration config,
        string? configSection = null)
        where TService : ConsumerBackgroundService<TMessage>
        where TMessage : class
    {
        if (configSection is not null)
            services.Configure<ConsumerBackgroundServiceOptions>(
                config.GetSection(configSection));
        else
            services.Configure<ConsumerBackgroundServiceOptions>(_ => { });

        services.AddHostedService<TService>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="OutboxRelayBackgroundService{TContext}"/> for the given
    /// <typeparamref name="TContext"/>.
    ///
    /// Requires <typeparamref name="TContext"/> to expose a <c>DbSet&lt;OutboxMessage&gt;</c>
    /// and to be registered in DI (via <see cref="AddPostgres{TContext}"/> or
    /// <see cref="AddPostgresWithReadReplica{TContext}"/>).
    ///
    /// Usage:
    /// <code>
    /// services.AddOutboxRelay&lt;OrderDbContext&gt;(config);
    /// </code>
    /// </summary>
    public static IServiceCollection AddOutboxRelay<TContext>(
        this IServiceCollection services,
        IConfiguration config,
        string configSection = "BackgroundServices:OutboxRelay")
        where TContext : DbContext
    {
        services.Configure<ConsumerBackgroundServiceOptions>(
            config.GetSection(configSection));

        services.AddHostedService<OutboxRelayBackgroundService<TContext>>();
        return services;
    }

    // ── Memory management / scalability ──────────────────────────────────────

    /// <summary>
    /// Registers platform-wide object pools for reusable, allocation-heavy types.
    ///
    /// Currently pooled:
    /// • <c>ObjectPool&lt;StringBuilder&gt;</c> — reuses string-builder instances across
    ///   request scopes to avoid per-request heap allocations for message formatting,
    ///   report generation, or dynamic SQL construction.
    ///
    /// Inject <c>ObjectPool&lt;StringBuilder&gt;</c> directly in a service:
    /// <code>
    /// public class ReportService(ObjectPool&lt;StringBuilder&gt; pool)
    /// {
    ///     public string Build()
    ///     {
    ///         var sb = pool.Get();
    ///         try   { sb.Append(...); return sb.ToString(); }
    ///         finally { pool.Return(sb); }
    ///     }
    /// }
    /// </code>
    /// </summary>
    public static IServiceCollection AddObjectPools(this IServiceCollection services)
    {
        // DefaultObjectPoolProvider uses ArrayPool<char> internally for StringBuilder,
        // so each Get() returns a cleared, pre-allocated builder from the pool.
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton(sp =>
            sp.GetRequiredService<ObjectPoolProvider>().CreateStringBuilderPool());
        return services;
    }

    /// <summary>
    /// Registers a bounded <see cref="IMessageChannel{TMessage}"/> (Singleton) and a
    /// concrete <see cref="ChannelConsumerBackgroundService{TMessage}"/> (Hosted Service)
    /// for high-throughput in-process message passing.
    ///
    /// Producers write into the channel with near-zero overhead on the HTTP request path;
    /// the background consumer drains it with up to
    /// <see cref="ChannelConsumerOptions.MaxConcurrency"/> parallel workers.
    ///
    /// Usage:
    /// <code>
    /// services.AddMessageChannel&lt;NotificationMessage, NotificationChannelConsumer&gt;(
    ///     config, "Channels:Notification");
    /// </code>
    /// </summary>
    /// <typeparam name="TMessage">The message type flowing through the channel.</typeparam>
    /// <typeparam name="TConsumer">
    /// The concrete <see cref="ChannelConsumerBackgroundService{TMessage}"/> subclass.
    /// </typeparam>
    /// <param name="configSection">
    /// Configuration section path binding to <see cref="ChannelConsumerOptions"/>.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    public static IServiceCollection AddMessageChannel<TMessage, TConsumer>(
        this IServiceCollection services,
        IConfiguration config,
        string? configSection = null)
        where TMessage : class
        where TConsumer : ChannelConsumerBackgroundService<TMessage>
    {
        if (configSection is not null)
            services.Configure<ChannelConsumerOptions>(config.GetSection(configSection));
        else
            services.Configure<ChannelConsumerOptions>(_ => { });

        // Singleton: the channel must outlive DI scopes so producers and the consumer
        // background service always share the same bounded channel instance.
        services.AddSingleton<IMessageChannel<TMessage>, BoundedMessageChannel<TMessage>>();
        services.AddHostedService<TConsumer>();
        return services;
    }
}
