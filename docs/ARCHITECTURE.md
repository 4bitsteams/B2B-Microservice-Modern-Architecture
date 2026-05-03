# Architecture Technical Documentation — B2B Microservice Platform

| Field | Value |
|---|---|
| Document type | Architecture Technical Reference |
| Audience | Backend engineers, architects, tech leads |
| Companion docs | [HLD.md](HLD.md) · [LLD.md](LLD.md) · [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md) |
| Last revised | 2026-05-03 |

---

## Table of Contents

1. [Solution Structure](#1-solution-structure)
2. [Dependency Graph](#2-dependency-graph)
3. [Shared Core Abstractions](#3-shared-core-abstractions)
4. [MediatR Pipeline — Deep Dive](#4-mediatr-pipeline--deep-dive)
5. [Resilience Pipeline — Polly 8](#5-resilience-pipeline--polly-8)
6. [Persistence Layer](#6-persistence-layer)
7. [Caching Architecture](#7-caching-architecture)
8. [Multi-Tenancy Implementation](#8-multi-tenancy-implementation)
9. [Event-Driven Architecture](#9-event-driven-architecture)
10. [Order Fulfillment Saga](#10-order-fulfillment-saga)
11. [API Gateway](#11-api-gateway)
12. [Security Architecture](#12-security-architecture)
13. [Observability Stack](#13-observability-stack)
14. [Service Catalogue — Technical Detail](#14-service-catalogue--technical-detail)
15. [Configuration Reference](#15-configuration-reference)
16. [Dependency Injection Map](#16-dependency-injection-map)

---

## 1. Solution Structure

```
B2B Microservice Modern Architecture/
├── B2B.sln
├── Directory.Packages.props          ← centralised NuGet version pins
├── Directory.Build.props
├── docker-compose.yml                ← full infra stack definition
├── docker-compose.override.yml       ← local dev env vars
│
├── src/
│   ├── Gateway/
│   │   └── B2B.Gateway/              ← YARP reverse proxy :5000
│   │
│   ├── Shared/
│   │   ├── B2B.Shared.Core/          ← Domain abstractions (zero infra deps)
│   │   │   ├── Common/               Result, Error, PagedList, FieldLengths
│   │   │   ├── CQRS/                 ICommand, IQuery, IIdempotentCommand
│   │   │   ├── Domain/               Entity, AggregateRoot, ValueObject, IDomainEvent
│   │   │   ├── Interfaces/           ICacheService, ICurrentUser, IEventBus,
│   │   │   │                         IRepository, IUnitOfWork, IPasswordHasher,
│   │   │   │                         IAuditableEntity, ITenantEntity, IAuthorizer,
│   │   │   │                         ITaxService, IPricingService, IAuditService,
│   │   │   │                         IDistributedLock, INotificationService
│   │   │   ├── Messaging/            IntegrationEvents.cs (all contracts)
│   │   │   └── Specifications/       ISpecification, BaseSpecification
│   │   │
│   │   └── B2B.Shared.Infrastructure/  ← Concrete adapters
│   │       ├── Behaviors/            LoggingBehavior, RetryBehavior,
│   │       │                         CommandBulkheadProvider, IdempotencyBehavior,
│   │       │                         PerformanceBehavior, AuthorizationBehavior,
│   │       │                         ValidationBehavior, AuditBehavior,
│   │       │                         DomainEventBehavior, RetryBehaviorOptions
│   │       ├── Caching/              RedisCacheService
│   │       ├── Http/                 CurrentUserService, ApiControllerBase,
│   │       │                         CorrelationIdMiddleware, CorrelationIdProvider
│   │       ├── Locking/              RedisDistributedLock
│   │       ├── Messaging/            MassTransitEventBus
│   │       ├── Notifications/        CompositeNotificationService
│   │       ├── Persistence/          BaseDbContext, BaseRepository
│   │       ├── Security/             BcryptPasswordHasher
│   │       ├── Services/             PercentageTaxService, TieredPricingService,
│   │       │                         SerilogAuditService
│   │       └── Extensions/           ServiceCollectionExtensions (all DI wiring)
│   │
│   └── Services/
│       ├── Identity/   { Domain | Application | Infrastructure | Api }   :5001
│       ├── Product/    { Domain | Application | Infrastructure | Api }   :5002
│       ├── Order/      { Domain | Application | Infrastructure | Api }   :5003
│       ├── Basket/     { Domain | Application | Infrastructure | Api }   :5004
│       ├── Payment/    { Domain | Application | Infrastructure | Api }   :5005
│       ├── Shipping/   { Domain | Application | Infrastructure | Api }   :5006
│       ├── Vendor/     { Domain | Application | Infrastructure | Api }   :5007
│       ├── Discount/   { Domain | Application | Infrastructure | Api }   :5008
│       └── Review/     { Domain | Application | Infrastructure | Api }   :5011
│
├── workers/
│   └── B2B.Notification.Worker/      ← MassTransit consumers, SMTP
│
├── tests/
│   ├── B2B.Identity.Tests/           135 tests
│   ├── B2B.Product.Tests/            55 tests
│   ├── B2B.Order.Tests/              69 tests
│   ├── B2B.Shared.Tests/             12 tests
│   ├── B2B.Basket.Tests/             52 tests
│   ├── B2B.Payment.Tests/            70 tests
│   ├── B2B.Shipping.Tests/           23 tests
│   ├── B2B.Discount.Tests/           76 tests
│   ├── B2B.Review.Tests/             35 tests
│   └── B2B.Vendor.Tests/             59 tests
│
└── infrastructure/
    ├── postgres/init.sql
    ├── otel/collector.yaml
    ├── prometheus/prometheus.yml
    └── grafana/provisioning/
        ├── datasources/datasources.yaml
        └── dashboards/dashboards.yaml
```

---

## 2. Dependency Graph

### Intra-service dependency rule

```
                ┌──────────────────────────────┐
                │       B2B.{Svc}.Api           │
                │  Program.cs, Controllers      │
                └──────────────┬───────────────┘
                               │ references
                ┌──────────────▼───────────────┐
                │  B2B.{Svc}.Infrastructure    │
                │  DbContext, Repos, Services   │
                └──────────────┬───────────────┘
                               │ references
                ┌──────────────▼───────────────┐
                │   B2B.{Svc}.Application       │
                │   Commands, Queries, Handlers │
                └──────────────┬───────────────┘
                               │ references
                ┌──────────────▼───────────────┐
                │     B2B.{Svc}.Domain          │
                │   Entities, Events, VOs       │
                └──────────────┬───────────────┘
                               │ references
                ┌──────────────▼───────────────┐
                │       B2B.Shared.Core         │
                │   Abstractions, Interfaces    │
                └──────────────────────────────┘
```

**Rules enforced at build time:**
- `Domain` → `B2B.Shared.Core` only (no Infrastructure)
- `Application` → `Domain` + `B2B.Shared.Core` only
- `Infrastructure` → `Application` + `B2B.Shared.Infrastructure`
- `Api` → `Infrastructure` (wires DI; no business logic)

A reverse reference causes a **build break** — circular dependency or Clean Architecture violation.

### Cross-service reference policy

Services **never** reference each other's assemblies. Cross-service communication uses:
- **Integration events** via RabbitMQ (async, decoupled)
- **`B2B.Shared.Core/Messaging/IntegrationEvents.cs`** as the single shared contract

---

## 3. Shared Core Abstractions

### 3.1 Result Pattern

`Result` and `Result<TValue>` are discriminated unions — business failures are values, not exceptions.

```csharp
// Non-generic: for commands that return no payload
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static implicit operator Result(Error error) => Failure(error);
}

// Generic: for commands/queries that return a payload
public class Result<TValue> : Result
{
    public TValue Value { get; }  // throws if IsFailure

    public static implicit operator Result<TValue>(TValue value) => Success(value);
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
```

**Implicit conversion** means handlers can return values or errors directly:

```csharp
// These are all valid return statements in a handler:
return new CreateOrderResponse(order.Id, order.OrderNumber);  // success
return Error.NotFound("Order.NotFound", $"Order {id} not found.");  // failure
return Error.Validation("Order.Empty", "No items.");  // failure
```

### 3.2 Error Type System

```csharp
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static Error NotFound(string code, string description)         → ErrorType.NotFound        → HTTP 404
    public static Error Validation(string code, string description)       → ErrorType.Validation      → HTTP 400
    public static Error Conflict(string code, string description)         → ErrorType.Conflict        → HTTP 409
    public static Error Unauthorized(string code, string description)     → ErrorType.Unauthorized    → HTTP 401
    public static Error Forbidden(string code, string description)        → ErrorType.Forbidden       → HTTP 403
    public static Error Failure(string code, string description)          → ErrorType.Failure         → HTTP 500
    public static Error ServiceUnavailable(string code, string description)→ ErrorType.ServiceUnavailable→ HTTP 503
}
```

`ApiControllerBase.ToActionResult()` maps `ErrorType` to HTTP status — controllers contain zero `if/switch` on error types.

### 3.3 CQRS Interfaces

```csharp
// Commands — mutate state
public interface ICommand : IRequest<Result>;
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

// Commands with idempotency — same key within 24h returns original result
public interface IIdempotentCommand { string IdempotencyKey { get; } }

// Queries — read-only, no side effects
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

// Handler interfaces (type aliases for MediatR)
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
```

### 3.4 Domain Building Blocks

```csharp
// Entity — identity-based equality
public abstract class Entity<TId> : IEquatable<Entity<TId>>
{
    public TId Id { get; protected init; }
    // Equality: Id-based, not reference-based
}

// AggregateRoot — consistency boundary + domain event queue
public abstract class AggregateRoot<TId> : Entity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent e) => _domainEvents.Add(e);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

// ValueObject — component-based equality
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();
    // Equality: all components equal
}

// Domain Event — immutable, raised inside aggregates
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
```

### 3.5 Port Interfaces

| Interface | Location | Implementations |
|---|---|---|
| `IRepository<TEntity, TId>` | `Shared.Core` | `BaseRepository<T, TId>` (EF Core) |
| `IReadRepository<TEntity, TId>` | `Shared.Core` | `OrderReadRepository`, `ProductReadRepository` (NoTracking, factory) |
| `IUnitOfWork` | `Shared.Core` | Each service's `DbContext` implements it |
| `ICacheService` | `Shared.Core` | `RedisCacheService` |
| `IEventBus` | `Shared.Core` | `MassTransitEventBus` |
| `ICurrentUser` | `Shared.Core` | `CurrentUserService` (from `IHttpContextAccessor`) |
| `IPasswordHasher` | `Shared.Core` | `BcryptPasswordHasher` |
| `IAuditableEntity` | `Shared.Core` | All entities implement it |
| `ITenantEntity` | `Shared.Core` | Domain aggregates implement it |
| `IAuthorizer<TCommand>` | `Shared.Core` | Per-command authorizer classes |
| `IDistributedLock` | `Shared.Core` | `RedisDistributedLock` |
| `ITaxService` | `Shared.Core` | `PercentageTaxService` |
| `IPricingService` | `Shared.Core` | `TieredPricingService` |
| `IAuditService` | `Shared.Core` | `SerilogAuditService` |
| `INotificationService` | `Shared.Core` | `CompositeNotificationService` |
| `ICorrelationIdProvider` | `Shared.Core` | `CorrelationIdProvider` |

---

## 4. MediatR Pipeline — Deep Dive

### 4.1 Registration order and execution model

```csharp
// ServiceCollectionExtensions.AddMediatRWithBehaviors
cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));        // 1 — outermost
cfg.AddOpenBehavior(typeof(RetryBehavior<,>));          // 2
cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));    // 3
cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));    // 4
cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));  // 5
cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));     // 6
cfg.AddOpenBehavior(typeof(AuditBehavior<,>));          // 7
cfg.AddOpenBehavior(typeof(DomainEventBehavior<,>));    // 8 — innermost
//                                                         Handler
```

MediatR calls each behavior in registration order, each wrapping the next via `await next()`. The innermost behavior calls the handler.

### 4.2 LoggingBehavior

**Position:** Outermost. Measures total round-trip including all other behaviors and the handler.

```csharp
// Logs before and after — timing spans the entire pipeline
logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
var response = await next();
logger.LogInformation("Handled {RequestName} in {Elapsed}ms", typeof(TRequest).Name, elapsed);
```

### 4.3 RetryBehavior

**Position:** 2nd. Applies only to `ICommand` / `ICommand<T>` — queries bypass it.

Full implementation detail in [§5](#5-resilience-pipeline--polly-8).

### 4.4 IdempotencyBehavior

**Position:** 3rd. Applies only to `IIdempotentCommand` requests.

```
Request arrives with IdempotencyKey
         │
         ▼
   cacheKey = "idem:{RequestType.FullName}:{IdempotencyKey}"
         │
    GET from Redis
         │
    ┌────▼────┐
    │ Hit?    │
    │  YES    │──────▶ Deserialize IdempotencyRecord → return cached Result
    │   NO    │──────▶ await next() → handler executes
    └─────────┘
         │
    IsSuccess?
    │  YES   │──────▶ SET in Redis (TTL 24h) → return
    │   NO   │──────▶ return failure (failures remain retryable)
```

Cache key namespacing: `idem:{FullTypeName}:{Key}` — different command types with the same key are independent.

Only **successful** results are cached. A failed command with the same key will re-execute on the next request.

### 4.5 PerformanceBehavior

Measures elapsed time. Logs a `Warning` when the handler exceeds the configured threshold (default: 200ms for queries, 500ms for commands).

### 4.6 AuthorizationBehavior

```csharp
// Resolves IAuthorizer<TRequest> from DI (registered per-command in service DI)
var authorizer = serviceProvider.GetService<IAuthorizer<TRequest>>();

if (authorizer is null) return await next();  // no authorizer = publicly accessible

var result = await authorizer.AuthorizeAsync(request, ct);
if (!result.IsAuthorized)
    return Error.Forbidden("Auth.Forbidden", result.FailureMessage);
```

`IAuthorizer<TCommand>` implementations can check ownership (`order.CustomerId == currentUser.UserId`), roles, and tenant membership.

### 4.7 ValidationBehavior

```csharp
// Auto-discovered via assembly scan in AddValidators(assemblies)
var validators = serviceProvider.GetServices<IValidator<TRequest>>();
if (!validators.Any()) return await next();

var failures = validators
    .SelectMany(v => v.Validate(request).Errors)
    .Where(f => f != null)
    .ToList();

if (failures.Count != 0)
    return Error.Validation("Validation.Failed", string.Join("; ", failures.Select(f => f.ErrorMessage)));
```

Short-circuits on first validation failure. The handler never executes if validation fails.

### 4.8 AuditBehavior

Writes a structured audit record **after** authorization and validation pass (so only legitimate requests are audited):

```csharp
var record = new AuditRecord(
    RequestType: typeof(TRequest).Name,
    UserId:      currentUser.UserId,
    TenantId:    currentUser.TenantId,
    OccurredAt:  DateTime.UtcNow,
    Payload:     JsonSerializer.Serialize(request));

await auditService.WriteAsync(record, ct);
```

`SerilogAuditService` writes to the Serilog structured log sink (Seq) — queryable by `UserId`, `TenantId`, `RequestType`.

### 4.9 DomainEventBehavior

Runs **after** the handler returns, ensuring domain events are only dispatched for committed state:

```csharp
var response = await next();  // handler + SaveChangesAsync happens here

// Scan EF ChangeTracker for any AggregateRoot<Guid> with pending events
var aggregates = dbContext.ChangeTracker
    .Entries<AggregateRoot<Guid>>()
    .Where(e => e.Entity.DomainEvents.Count != 0)
    .Select(e => e.Entity)
    .ToList();

var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
aggregates.ForEach(a => a.ClearDomainEvents());  // drain before publishing

foreach (var domainEvent in events)
    await publisher.Publish(domainEvent, ct);   // MediatR in-process INotification
```

Domain event handlers typically convert domain events to integration events and publish them via `IEventBus`.

---

## 5. Resilience Pipeline — Polly 8

### 5.1 Architecture

`RetryBehavior` applies a three-layer pipeline to every `ICommand` request:

```
ICommand request
      │
      ▼
┌─────────────────────────────────────────────────────────────┐
│  LAYER 1: BULKHEAD (SemaphoreSlim, non-Polly)               │
│                                                             │
│  Provider: CommandBulkheadProvider (singleton)              │
│  One SemaphoreSlim per TRequest type                        │
│  Capacity: BulkheadMaxConcurrency (default: 100)            │
│  Queue: 0 (reject immediately, never queue)                 │
│                                                             │
│  _bulkhead.WaitAsync(0) → false?                            │
│    → Error.ServiceUnavailable("Bulkhead.Full", ...)  HTTP 503│
│    → return immediately (no retry, no CB evaluation)        │
└─────────────────┬───────────────────────────────────────────┘
                  │ slot acquired
                  ▼
┌─────────────────────────────────────────────────────────────┐
│  LAYER 2: CIRCUIT BREAKER (Polly ResiliencePipeline)        │
│                                                             │
│  Tracks: IOException, TimeoutException,                     │
│           InvalidOperationException (connection/timeout msg)│
│  Opens when: FailureRatio ≥ 0.5 over SamplingDuration 10s  │
│              AND MinimumThroughput ≥ 5 calls in window      │
│  Break duration: 30s (then moves to HALF-OPEN)              │
│  Half-open: sends 1 probe request                           │
│                                                             │
│  Open? → BrokenCircuitException                             │
│    → Error.ServiceUnavailable("CircuitBreaker.Open", ...)   │
│    → _bulkhead.Release() in finally block                   │
└─────────────────┬───────────────────────────────────────────┘
                  │ circuit closed or half-open probe
                  ▼
┌─────────────────────────────────────────────────────────────┐
│  LAYER 3: RETRY (Polly RetryStrategyOptions)                 │
│                                                             │
│  MaxRetryAttempts: 3                                        │
│  Backoff: Exponential + jitter                              │
│  Base delay: 200ms → ~400ms → ~800ms (+ jitter)             │
│  Handles: same exception types as Circuit Breaker           │
│                                                             │
│  Each failure increments CB failure counter                 │
│  Successful retry resets CB failure counter                 │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
              next() → Handler
                  │
finally: _bulkhead.Release()
```

### 5.2 CommandBulkheadProvider

```csharp
// Singleton — one instance for the application lifetime
public sealed class CommandBulkheadProvider
{
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _semaphores = new();

    // Called once per closed generic RetryBehavior<TRequest, TResponse> instance
    public SemaphoreSlim GetOrCreate<TRequest>(int maxConcurrency) =>
        _semaphores.GetOrAdd(typeof(TRequest),
            _ => new SemaphoreSlim(maxConcurrency, maxConcurrency));
}
```

Each command type (`CreateOrderCommand`, `CancelOrderCommand`, etc.) gets its own semaphore. A surge of `CreateOrderCommand` requests does not block `CancelOrderCommand` concurrency.

### 5.3 Configuration

```json
// appsettings.json — all fields optional; shown values are defaults
"RetryBehavior": {
  "MaxRetryAttempts": 3,
  "InitialDelayMs": 200,
  "UseJitter": true,
  "CircuitBreakerMinimumThroughput": 5,
  "CircuitBreakerFailureRatio": 0.5,
  "CircuitBreakerSamplingDurationSeconds": 10,
  "CircuitBreakerBreakDurationSeconds": 30,
  "BulkheadMaxConcurrency": 100,
  "BulkheadQueueLimit": 0
}
```

### 5.4 Circuit Breaker State Transitions

```
         Initial
            │
            ▼
┌────────── CLOSED ──────────┐
│  Normal operation          │
│  Failure counter running   │
│  FailureRatio ≥ threshold? │──────▶ OPEN
└────────────────────────────┘         │
                                       │ BreakDuration elapsed
                                       ▼
                              ┌─── HALF-OPEN ───┐
                              │  1 probe request │
                              │  Success? ───────┤──────▶ CLOSED
                              │  Failure? ───────┘──────▶ OPEN
                              └──────────────────┘
```

State transitions logged at `Error` (OPEN) and `Information` (CLOSED / HALF-OPEN).

---

## 6. Persistence Layer

### 6.1 BaseDbContext

All service DbContexts extend `BaseDbContext`:

```csharp
public abstract class BaseDbContext(DbContextOptions options, IServiceProvider serviceProvider)
    : DbContext(options)
{
    // Resolved lazily per query — safe for background services (returns Guid.Empty)
    private Guid CurrentTenantId =>
        serviceProvider?.GetService<ICurrentUser>()?.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply global tenant filter to every entity implementing ITenantEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(BaseDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
        }

        // Apply audit interceptor, configurations, etc.
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        // Guid.Empty sentinel: passes all rows (background workers, migrations)
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || CurrentTenantId == Guid.Empty);
    }
}
```

### 6.2 Read/Write Split

Services with high read load use `AddPostgresWithReadReplica<TContext>`:

```
┌────────────────────────────┐    ┌────────────────────────────────────────┐
│  IDbContextFactory<TCtx>   │    │  TContext (scoped)                      │
│  Singleton                 │    │  Scoped                                 │
│  Read replica connection   │    │  Primary connection                     │
│  QueryTrackingBehavior     │    │  Change tracking ON                     │
│    .NoTracking             │    │  Used by: repositories, IUnitOfWork     │
│  Used by: read repositories│    │  Used by: command handlers              │
└────────────────────────────┘    └────────────────────────────────────────┘
         ▲                                   ▲
         │                                   │
   ReadRepository                    OrderRepository
   (query handlers)                  (command handlers)
```

If `ReadReplicaConnection` is not configured, the factory falls back to `DefaultConnection` — single-node dev works without changes.

### 6.3 Connection Pooling (PgBouncer)

```
Service Pod (Npgsql)
    │
    │ connects to PgBouncer :6432
    ▼
┌──────────────────────────────────────────┐
│           PgBouncer                      │
│  pool_mode = transaction                 │
│  max_client_conn = 5000                  │
│  default_pool_size = 50 per DB           │
│  max_db_connections = 200               │
└──────────────────────────────────────────┘
    │
    │ multiplexed to PostgreSQL :5432
    ▼
┌──────────────────────────────────────────┐
│           PostgreSQL 16                  │
│  max_connections = 200                   │
└──────────────────────────────────────────┘
```

**Transaction mode:** Connection is held only for the duration of a transaction/statement, then returned to the pool. This allows 5,000 application-level connections sharing 200 backend connections.

**Important:** Transaction mode is incompatible with EF Core prepared statements. Set `No Reset On Close=true` in the Npgsql connection string when pointing at PgBouncer.

### 6.4 EF Core Configuration

```csharp
opt.UseNpgsql(connStr, npg =>
{
    npg.EnableRetryOnFailure(3);         // transient fault retry
    npg.CommandTimeout(30);              // seconds; below HTTP gateway timeout
    npg.MinBatchSize(1);                 // avoid batching issues with PgBouncer
})
.EnableDetailedErrors(false)            // never true in production
.EnableSensitiveDataLogging(false)      // never true in production
```

### 6.5 Migrations

Each service owns its migrations under `{Service}.Infrastructure/Persistence/Migrations/`.

```bash
# From the Infrastructure project directory
dotnet ef migrations add AddOrderIndex \
    --startup-project ../B2B.Order.Api

dotnet ef database update \
    --startup-project ../B2B.Order.Api
```

All databases created by `infrastructure/postgres/init.sql` before migrations run.

---

## 7. Caching Architecture

### 7.1 Two-layer model

```
Query Handler Request
        │
        ▼
┌───────────────────────────────────────────────────────┐
│              HybridCache (L1 + L2)                    │
│                                                       │
│  L1: In-process memory                                │
│      TTL: 2 minutes                                   │
│      Scope: per pod replica                           │
│      Latency: ~0ms (no network)                       │
│      Stampede: built-in coalescing                    │
│                                                       │
│  L2: Redis distributed cache                          │
│      TTL: 15 minutes                                  │
│      Scope: shared across all replicas                │
│      Latency: ~1-2ms (network)                        │
│      Serialization: binary/JSON                       │
└───────────┬───────────────────────────────────────────┘
            │ L1 miss + L2 miss
            ▼
┌───────────────────────────────────────────────────────┐
│           ICacheService (Redis cache-aside)            │
│                                                       │
│  Manual TTL control                                   │
│  Prefix-based invalidation (SCAN + DEL)               │
│  Failure-tolerant (swallows transient errors)         │
└───────────┬───────────────────────────────────────────┘
            │ cache miss
            ▼
     Database / Repository
```

### 7.2 HybridCache Usage Pattern

```csharp
// Inject Microsoft.Extensions.Caching.Hybrid.HybridCache directly
public sealed class GetProductsHandler(
    HybridCache hybridCache,
    IReadProductRepository repo,
    ICurrentUser currentUser) : IQueryHandler<GetProductsQuery, PagedList<ProductDto>>
{
    public async Task<Result<PagedList<ProductDto>>> Handle(GetProductsQuery q, CancellationToken ct)
    {
        var key = $"products:tenant:{currentUser.TenantId}:page:{q.Page}:size:{q.PageSize}";

        var result = await hybridCache.GetOrCreateAsync(
            key,
            async cancel => await repo.GetPagedAsync(currentUser.TenantId, q.Page, q.PageSize, cancel),
            cancellationToken: ct);

        return result;
    }
}
```

**Stampede protection:** When multiple concurrent requests miss the same key simultaneously, `HybridCache` serializes factory calls — only **one** request fetches from the database. Others await the single inflight result.

### 7.3 ICacheService Usage Pattern

```csharp
// For scenarios requiring custom TTL or prefix-based invalidation
// On read:
var orders = await cache.GetOrCreateAsync(
    key:     $"orders:tenant:{tenantId}:customer:{customerId}:page:{page}",
    factory: async () => await repo.GetByCustomerAsync(...),
    expiry:  TimeSpan.FromMinutes(2));

// On write (invalidate all pages for this tenant):
await cache.RemoveByPrefixAsync($"orders:tenant:{tenantId}");
```

`RedisCacheService.RemoveByPrefixAsync` uses Redis `SCAN` + `DEL` — safe on large keyspaces.

### 7.4 Output Cache

```csharp
// Registered in AddSharedInfrastructure
opt.AddPolicy("queries", policy => policy
    .Expire(TimeSpan.FromSeconds(30))
    .SetVaryByQuery("page", "pageSize", "tenantId")
    .Tag("queries"));

// Applied to controller endpoints
[HttpGet]
[OutputCache(PolicyName = "queries")]
public async Task<IActionResult> GetAll([FromQuery] GetProductsQuery query) { ... }
```

Output cache sits **after** authentication middleware — cached responses respect authorization.

---

## 8. Multi-Tenancy Implementation

### 8.1 ITenantEntity

```csharp
// B2B.Shared.Core/Interfaces/ITenantEntity.cs
public interface ITenantEntity
{
    Guid TenantId { get; }
}
```

All domain aggregates implement this interface:

```csharp
public class Order : AggregateRoot<Guid>, IAuditableEntity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    // ...
}
```

### 8.2 Global Query Filter (BaseDbContext)

Applied automatically in `OnModelCreating` via reflection — no per-entity configuration needed:

```
OnModelCreating scans all entity types
    │
    └── For each type implementing ITenantEntity
            │
            └── HasQueryFilter(e => e.TenantId == CurrentTenantId || CurrentTenantId == Guid.Empty)
```

`CurrentTenantId`:
- **HTTP requests:** resolved from `ICurrentUser.TenantId` (JWT `tenant_id` claim)
- **Background services:** returns `Guid.Empty` → filter expression evaluates to `true` → all rows returned
- **Migrations:** EF migration context has no HTTP context → `Guid.Empty` → all rows visible

### 8.3 Cross-Tenant Background Queries

Background workers must call `IgnoreQueryFilters()` explicitly. The global filter passes when `CurrentTenantId == Guid.Empty` (background), but `IgnoreQueryFilters()` makes the intent explicit and documents the bypass:

```csharp
// StuckSagaCleanupWorker — must scan ALL tenants
var stuckSagas = await dbContext.OrderFulfillmentSagas
    .IgnoreQueryFilters()   // ← explicit cross-tenant scan
    .Where(s =>
        (s.CurrentState == "AwaitingStockReservation" && ...) ||
        (s.CurrentState == "AwaitingPayment" && ...) ||
        (s.CurrentState == "AwaitingShipment" && ...))
    .ToListAsync(ct);
```

### 8.4 ICurrentUser

```csharp
// Resolved from IHttpContextAccessor in CurrentUserService
public class CurrentUserService : ICurrentUser
{
    public Guid UserId { get; }        // from "sub" claim
    public Guid TenantId { get; }      // from "tenant_id" claim
    public string TenantSlug { get; }  // from "tenant_slug" claim
    public IReadOnlyList<string> Roles { get; } // from "role" claims
    public bool IsAuthenticated { get; }
    public bool IsInRole(string role) => Roles.Contains(role);
}
```

Registered as `Scoped` — a new instance per HTTP request. Background services injecting `ICurrentUser` will get `IsAuthenticated = false` and `TenantId = Guid.Empty`.

---

## 9. Event-Driven Architecture

### 9.1 Event types

| Type | Transport | Lifetime | Example |
|---|---|---|---|
| **Domain Event** | MediatR `INotification` | In-process only | `OrderConfirmedEvent` |
| **Integration Event** | RabbitMQ via MassTransit | Cross-service | `OrderConfirmedIntegration` |
| **Saga Command** | RabbitMQ (typed endpoint) | Orchestrator → participant | `ReserveStockCommand` |

### 9.2 Flow: Domain Event → Integration Event

```
1. Command handler mutates aggregate
        │
        ▼
2. SaveChangesAsync commits to PostgreSQL
        │
        ▼
3. DomainEventBehavior scans ChangeTracker
        │
        ▼
4. Publishes OrderConfirmedEvent (in-process MediatR)
        │
        ▼
5. WhenOrderConfirmed_PublishIntegration handler receives it
        │
        ▼
6. Calls IEventBus.PublishAsync(new OrderConfirmedIntegration(...))
        │
        ▼
7. MassTransitEventBus publishes to RabbitMQ exchange
        │
        ▼
8. RabbitMQ delivers to:
   ├── Notification Worker queue → sends email
   └── Other service queues (if subscribed)
```

### 9.3 MassTransit Configuration

```csharp
// AddEventBus — shared configuration for all services
services.AddMassTransit(x =>
{
    configureConsumers?.Invoke(x);   // per-service consumers/sagas

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(host, vhost, h => { h.Username(...); h.Password(...); });

        cfg.UseMessageRetry(r =>
            r.Intervals(100, 500, 1_000, 2_000, 5_000));  // 5 retries

        cfg.UseInMemoryOutbox(ctx);   // at-least-once within handler scope

        configureRabbitMq?.Invoke(cfg);  // per-service extras (e.g. delayed scheduler)

        cfg.ConfigureEndpoints(ctx);  // auto-configure from registered consumers
    });
});
```

### 9.4 Integration Event Contracts

All contracts in `B2B.Shared.Core/Messaging/IntegrationEvents.cs`:

```csharp
// Order lifecycle
public sealed record OrderConfirmedIntegration(
    Guid OrderId, string OrderNumber, Guid CustomerId, Guid TenantId,
    string CustomerEmail, decimal TotalAmount, string Currency, DateTime ConfirmedAt);

public sealed record OrderFulfilledIntegration(
    Guid OrderId, string OrderNumber, Guid CustomerId, Guid TenantId,
    string CustomerEmail, string TrackingNumber);

public sealed record OrderCancelledDueToStockIntegration(...);
public sealed record OrderCancelledDueToPaymentIntegration(...);
public sealed record OrderCancelledDueToShipmentIntegration(...);
public sealed record OrderProcessingStartedIntegration(...);

// Cross-service
public sealed record BasketCheckedOutIntegration(
    Guid UserId, Guid TenantId, List<BasketItemDto> Items,
    string? CouponCode, AddressDto ShippingAddress);

public sealed record UserRegisteredIntegration(Guid UserId, string Email, string FullName, Guid TenantId);
public sealed record ProductLowStockIntegration(Guid ProductId, string Sku, int CurrentStock, Guid TenantId);
public sealed record StockReservedIntegration(Guid OrderId, bool Success, string? FailureReason);
public sealed record PaymentProcessedIntegration(Guid OrderId, Guid PaymentId, decimal Amount, bool Success);
public sealed record ShipmentCreatedIntegration(Guid OrderId, Guid ShipmentId, string TrackingNumber, bool Success);
```

---

## 10. Order Fulfillment Saga

### 10.1 State machine definition

`OrderFulfillmentSaga` is a MassTransit `MassTransitStateMachine<OrderFulfillmentSagaState>`. State is persisted to `b2b_order` via EF Core — the saga survives service restarts.

### 10.2 States

| State | Trigger | Next states |
|---|---|---|
| `Initial` | — | `AwaitingStockReservation` |
| `AwaitingStockReservation` | `OrderConfirmedIntegration` consumed | `AwaitingPayment` (success) · `Final` (fail/timeout) |
| `AwaitingPayment` | `StockReservedIntegration` success | `AwaitingShipment` (success) · `Final` (fail/timeout) |
| `AwaitingShipment` | `PaymentProcessedIntegration` success | `Final` (success or fail/timeout) |
| `Final` | Any terminal event | — |

### 10.3 Compensation chains

```
Stock failure / timeout
    → Publish OrderCancelledDueToStockIntegration
    → Move to Final

Payment failure / timeout
    → Publish ReleaseStockCommand (to Product service)
    → Publish OrderCancelledDueToPaymentIntegration
    → Move to Final

Shipment failure / timeout
    → Publish RefundPaymentCommand (to Payment service)
    → Publish ReleaseStockCommand (to Product service)
    → Publish OrderCancelledDueToShipmentIntegration
    → Move to Final
```

### 10.4 Timeout scheduling

Uses RabbitMQ delayed message exchange (via `UseDelayedMessageScheduler()`). When the saga enters `AwaitingStockReservation`, it schedules a `StockReservationTimeoutExpired` message to arrive after `StockReservationDeadline` (default 5 min). If the stock reply arrives first, the timeout is cancelled.

```csharp
// OrderFulfillmentSagaOptions — configurable per environment
public sealed class OrderFulfillmentSagaOptions
{
    public const string SectionName = "OrderFulfillmentSaga";
    public TimeSpan StockReservationDeadline { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan PaymentDeadline { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan ShipmentDeadline { get; set; } = TimeSpan.FromHours(2);
}
```

### 10.5 StuckSagaCleanupWorker

A `BackgroundService` in `B2B.Order.Infrastructure.Workers` that detects sagas the timeout logic missed (e.g., RabbitMQ delayed message exchange outage):

```
Every 5 minutes:
    Scan OrderFulfillmentSagas (IgnoreQueryFilters — all tenants)
    WHERE:
        (State == "AwaitingStockReservation" AND age > StockDeadline × 3) OR
        (State == "AwaitingPayment"           AND age > PaymentDeadline × 3) OR
        (State == "AwaitingShipment"          AND age > ShipmentDeadline × 3)
    FOR EACH stuck saga:
        Log.Warning("Stuck saga detected: OrderId={} State={} Age={}min")
    Log count at Warning level
```

The worker **does not mutate** saga state — MassTransit owns state transitions. Alerting on the `Warning` log triggers on-call investigation.

### 10.6 Concurrency

```csharp
// EntityFrameworkRepository config in Order.Infrastructure/DependencyInjection.cs
r.ConcurrencyMode = ConcurrencyMode.Optimistic;
r.ExistingDbContext<OrderDbContext>();
```

EF Core optimistic concurrency on saga rows prevents split-brain when multiple message copies arrive (broker retry scenario).

---

## 11. API Gateway

### 11.1 YARP Configuration

The gateway routes based on path prefix:

```json
{
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": { "Path": "/api/identity/{**catch-all}" },
        "Transforms": [{ "PathRemovePrefix": "/api/identity" }]
      }
      // ... one route per service
    },
    "Clusters": {
      "identity-cluster": {
        "Destinations": { "identity-primary": { "Address": "http://identity-service:8080" } },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }
        }
      }
    }
  }
}
```

### 11.2 Rate Limiting

Two policies active simultaneously:

**Per-IP fixed window** (baseline protection):
```csharp
options.AddFixedWindowLimiter("fixed", opt => {
    opt.Window = TimeSpan.FromMinutes(1);
    opt.PermitLimit = 300;
});
```

**Per-tenant sliding window** (hot-tenant isolation):
```csharp
options.AddSlidingWindowLimiter("per-tenant", opt => {
    opt.Window = TimeSpan.FromMinutes(1);
    opt.PermitLimit = 1000;
    opt.SegmentsPerWindow = 6;
}).WithPartition(ctx =>
    ctx.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId)
        ? tenantId.ToString()
        : ctx.Request.Headers.Authorization.ToString() is { Length: > 7 } auth
            ? ExtractTenantFromJwt(auth[7..])
            : ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown");
```

### 11.3 JWT Validation at Gateway

The gateway validates JWTs before forwarding. Downstream services re-validate (defence-in-depth):

```csharp
// Gateway validates: signature, issuer, audience, lifetime, ClockSkew = Zero
// Downstream services also validate with the same parameters
```

If a JWT expires between gateway validation and the downstream handler, the downstream service rejects it.

---

## 12. Security Architecture

### 12.1 JWT Structure

```json
{
  "sub":          "user-guid",
  "email":        "user@tenant.com",
  "tenant_id":    "tenant-guid",
  "tenant_slug":  "acme-corp",
  "role":         ["buyer", "admin"],
  "iat":          1746000000,
  "exp":          1746003600,
  "iss":          "b2b-identity",
  "aud":          "b2b-platform"
}
```

Algorithm: `HS256`. Secret from `JwtSettings:SecretKey` environment variable. TTL: 60 minutes. Clock skew: 0.

### 12.2 Refresh Tokens

- Stored in `b2b_identity` DB as hashed tokens
- Max 5 concurrent active tokens per user; oldest revoked on overflow
- All revoked on explicit logout
- Rotation: a new refresh token is issued on every use; the old one is invalidated

### 12.3 Authorization Layers

```
1. Gateway: JWT signature + claims (every request)
2. Service middleware: re-validates JWT (defence-in-depth)
3. AuthorizationBehavior: role check via ICurrentUser.IsInRole(...)
4. IAuthorizer<TCommand>: resource ownership check
         Example: CancelOrderAuthorizer checks order.CustomerId == currentUser.UserId
5. Global EF filter: TenantId scoping (data-layer isolation)
```

### 12.4 Password Security

```csharp
// IPasswordHasher — port in Shared.Core
string Hash(string password);       // BCrypt.HashPassword
bool Verify(string password, string hash); // BCrypt.Verify

// Never referenced directly from Application or Domain layers
// Only BcryptPasswordHasher (Infrastructure) uses BCrypt.Net
```

Cost factor is configurable; default matches OWASP recommendations.

---

## 13. Observability Stack

### 13.1 Signal flow

```
Service process
    │
    ├── Traces ──────────────▶ OTLP gRPC :4317
    ├── Metrics ─────────────▶ OTLP gRPC :4317
    └── Logs ────────────────▶ Serilog → Seq HTTP :5341
                                    │
                        OTel Collector (otel-collector:4317)
                                    │
                         ┌──────────┴──────────┐
                         ▼                     ▼
                     Jaeger              Prometheus scrape
                   (traces)               endpoint :8889
                  :16686                       │
                                               ▼
                                           Grafana
                                            :3000
```

### 13.2 Trace instrumentation

```csharp
.WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()        // HTTP request spans
    .AddHttpClientInstrumentation()        // outbound HTTP spans
    .AddEntityFrameworkCoreInstrumentation() // DB query spans
    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
```

W3C `traceparent` header propagates through:
- HTTP requests (YARP → services)
- RabbitMQ message headers (MassTransit propagates automatically)

Result: one `traceId` covers Gateway → Service → DB → RabbitMQ → Notification Worker → SMTP.

### 13.3 Metrics instrumentation

```csharp
.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()  // request rate, latency, active requests
    .AddHttpClientInstrumentation()  // outbound HTTP metrics
    .AddRuntimeInstrumentation()     // GC, thread pool, allocations, CPU
    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
```

OTel Collector receives OTLP metrics and exposes them on `:8889` for Prometheus scrape.

### 13.4 OTel Collector configuration

```yaml
# infrastructure/otel/collector.yaml
receivers:
  otlp:
    protocols:
      grpc: { endpoint: "0.0.0.0:4317" }
      http: { endpoint: "0.0.0.0:4318" }

processors:
  batch: {}
  memory_limiter:
    check_interval: 1s
    limit_mib: 512

exporters:
  jaeger:
    endpoint: "jaeger:14250"
    tls: { insecure: true }
  prometheus:
    endpoint: "0.0.0.0:8889"

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [jaeger]
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [prometheus]
```

### 13.5 Health endpoints

| Endpoint | Check type | Purpose |
|---|---|---|
| `GET /health/live` | None (Predicate = _ => false) | Liveness probe — confirms process is alive |
| `GET /health/ready` | Tags `"ready"` (PG + Redis + RabbitMQ) | Readiness probe — confirms dependencies reachable |
| `GET /health` | All checks | Legacy all-checks endpoint |

Response format: `UIResponseWriter.WriteHealthCheckUIResponse` (JSON with status per check).

### 13.6 Correlation ID

`CorrelationIdMiddleware` reads `X-Correlation-ID` from the inbound request (or generates a new `Guid`). The value is echoed in the response header and stored in `ICorrelationIdProvider` for use in log enrichment.

---

## 14. Service Catalogue — Technical Detail

### 14.1 Identity Service (:5001)

| Aspect | Detail |
|---|---|
| Database | `b2b_identity` |
| Key aggregates | `User`, `Tenant`, `RefreshToken` |
| JWT issuance | `TokenService` — HS256, 60min TTL, `tenant_id` + `tenant_slug` + `role` claims |
| Password | BCrypt via `IPasswordHasher`; never stored or logged as plaintext |
| Refresh | Max 5 concurrent tokens; oldest evicted; all revoked on logout |
| Notable handlers | `LoginHandler`, `RegisterUserHandler`, `RefreshTokenHandler`, `RegisterTenantHandler` |

### 14.2 Product Service (:5002)

| Aspect | Detail |
|---|---|
| Database | `b2b_product` |
| Key aggregates | `Product`, `Category` |
| Stock reservation | `StockReservationConsumer` + `ReleaseStockConsumer` run in Order process (in-proc for dev; separate process in production) |
| Low-stock events | `ProductLowStockIntegration` raised when stock falls below threshold |
| Cache | HybridCache on product list queries; prefix-based invalidation on mutations |

### 14.3 Order Service (:5003)

| Aspect | Detail |
|---|---|
| Database | `b2b_order` + saga state tables |
| Key aggregates | `Order`, `OrderItem` |
| Idempotency | `CreateOrderCommand : IIdempotentCommand` — 24h Redis cache |
| Saga | `OrderFulfillmentSaga` + `OrderFulfillmentSagaState` persisted in EF |
| Background worker | `StuckSagaCleanupWorker` — 5 min scan, 3× timeout threshold |
| Type aliases required | `OrderEntity`, `OrderItemEntity`, `OrderStatus` |

### 14.4 Basket Service (:5004)

| Aspect | Detail |
|---|---|
| Database | Redis only (no PostgreSQL) |
| Repository | `IBasketRepository` → `RedisBasketRepository` (JSON hash per user) |
| No IUnitOfWork | `SaveAsync` writes directly to Redis |
| Checkout | Publishes `BasketCheckedOutIntegration` → Order service creates `Order` |
| Ephemeral | Lost on Redis restart — by design (BRD FR-BS-7) |

### 14.5 Payment Service (:5005)

| Aspect | Detail |
|---|---|
| Database | `b2b_payment` |
| Key aggregates | `Payment`, `Invoice` |
| Gateway | `IPaymentGateway` → `StubPaymentGateway` (dev); replace with Stripe/Adyen adapter |
| Saga participant | Consumes `ProcessPaymentCommand`; publishes `PaymentProcessedIntegration` |
| Refund | `RefundPaymentConsumer` triggered by saga compensation |

### 14.6 Shipping Service (:5006)

| Aspect | Detail |
|---|---|
| Database | `b2b_shipping` |
| Key aggregate | `Shipment` |
| Tracking numbers | Auto-generated prefix `B2B-` + Guid segment |
| Saga participant | Consumes `CreateShipmentCommand`; publishes `ShipmentCreatedIntegration` |
| Gateway | `IShipmentGateway` → `StubShipmentGateway` (dev) |

### 14.7 Vendor Service (:5007)

| Aspect | Detail |
|---|---|
| Database | `b2b_vendor` |
| Key aggregate | `Vendor` |
| State machine | `PendingApproval → Active → Suspended → Active / Deactivated` |
| Authorization | `IAuthorizer<ApproveVendorCommand>` — platform admin only |
| Type aliases required | `VendorEntity`, `VendorStatus` |

### 14.8 Discount Service (:5008)

| Aspect | Detail |
|---|---|
| Database | `b2b_discount` |
| Key aggregates | `Discount`, `Coupon` |
| Discount types | `Percentage`, `Fixed`, `FreeShipping` |
| Coupon validation | Usage limit + expiry + tenant scope |
| Concurrency | Atomic Redis decrement on coupon apply |

### 14.9 Review Service (:5011)

| Aspect | Detail |
|---|---|
| Database | `b2b_review` |
| Key aggregate | `Review` |
| Uniqueness | One review per buyer per product (enforced in domain) |
| Moderation | `Pending → Approved / Rejected` |

### 14.10 Notification Worker

| Aspect | Detail |
|---|---|
| Transport | RabbitMQ competing consumer |
| Email | `IEmailService` → `SmtpEmailService` (dev: MailHog :8025) |
| Consumers | 9 consumer classes — one per integration event type |
| Retry | MassTransit retry intervals (100ms → 5s); dead-letter after all attempts |

---

## 15. Configuration Reference

### 15.1 Standard appsettings sections

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=pgbouncer;Port=6432;Database=b2b_{svc};Username=...;Password=...;No Reset On Close=true",
    "ReadReplicaConnection": "Host=pgbouncer;Port=6432;Database=b2b_{svc};...",
    "Redis": "redis:6379"
  },

  "JwtSettings": {
    "SecretKey": "...",
    "Issuer": "b2b-identity",
    "Audience": "b2b-platform",
    "ExpiryMinutes": 60
  },

  "RabbitMQ": {
    "Host": "rabbitmq",
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "guest"
  },

  "OpenTelemetry": {
    "Endpoint": "http://otel-collector:4317"
  },

  "RetryBehavior": {
    "MaxRetryAttempts": 3,
    "InitialDelayMs": 200,
    "UseJitter": true,
    "CircuitBreakerMinimumThroughput": 5,
    "CircuitBreakerFailureRatio": 0.5,
    "CircuitBreakerSamplingDurationSeconds": 10,
    "CircuitBreakerBreakDurationSeconds": 30,
    "BulkheadMaxConcurrency": 100,
    "BulkheadQueueLimit": 0
  },

  "OrderFulfillmentSaga": {
    "StockReservationDeadline": "00:05:00",
    "PaymentDeadline": "00:10:00",
    "ShipmentDeadline": "02:00:00"
  }
}
```

### 15.2 Configuration precedence

```
1. Environment variables (__ separator: "RabbitMQ__Host=rabbitmq")
2. appsettings.{ASPNETCORE_ENVIRONMENT}.json
3. appsettings.json
```

Local-dev values are in `docker-compose.override.yml`. Production secrets injected by orchestrator (Kubernetes secrets / AWS Parameter Store / Azure Key Vault). **Never committed to source control.**

---

## 16. Dependency Injection Map

### Shared Infrastructure registrations (per service, applied by `AddSharedInfrastructure`)

| Interface / Type | Concrete | Lifetime |
|---|---|---|
| MediatR pipeline behaviors (8) | `LoggingBehavior<,>` … `DomainEventBehavior<,>` | Transient |
| `CommandBulkheadProvider` | `CommandBulkheadProvider` | **Singleton** |
| `IValidator<T>` (all) | Auto-discovered from assemblies | Transient |
| `IConnectionMultiplexer` | `ConnectionMultiplexer` | **Singleton** |
| `ICacheService` | `RedisCacheService` | **Singleton** |
| `HybridCache` | Framework internal | **Singleton** |
| `ICurrentUser` | `CurrentUserService` | Scoped |
| `ICorrelationIdProvider` | `CorrelationIdProvider` | Scoped |
| `DbContext` | Service-specific `{Svc}DbContext` | Scoped |
| `IDbContextFactory<TContext>` | Service-specific (read replica) | **Singleton** |
| `IEventBus` | `MassTransitEventBus` | Scoped |
| `IDistributedLock` | `RedisDistributedLock` | **Singleton** |
| `ITaxService` | `PercentageTaxService` | **Singleton** |
| `IPricingService` | `TieredPricingService` | **Singleton** |
| `IAuditService` | `SerilogAuditService` | **Singleton** |
| `INotificationService` | `CompositeNotificationService` | Scoped |

### Per-service registrations (applied by `Add{Svc}Infrastructure`)

| Interface | Concrete | Lifetime |
|---|---|---|
| `IRepository<TEntity, Guid>` | `{Entity}Repository` (EF Core write) | Scoped |
| `IReadRepository<TEntity, Guid>` | `{Entity}ReadRepository` (NoTracking factory) | **Singleton** |
| `IUnitOfWork` | `{Svc}DbContext` (implements both) | Scoped |
| `IPasswordHasher` | `BcryptPasswordHasher` | Scoped (Identity only) |
| `IAuthorizer<TCommand>` | `{Command}Authorizer` (one per command) | Scoped |
| `IPaymentGateway` | `StubPaymentGateway` | Scoped (Payment only) |
| `IShipmentGateway` | `StubShipmentGateway` | Scoped (Shipping only) |
| `IOrderNumberGenerator` | `DefaultOrderNumberGenerator` | **Singleton** (Order only) |
| `StuckSagaCleanupWorker` | `StuckSagaCleanupWorker` | Hosted Service (Order only) |

---

*Document type: Architecture Technical Reference*
*Platform: B2B Microservice Modern Architecture*
*Last revised: 2026-05-03*
