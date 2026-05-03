# Developer Guide — B2B Microservice Platform

| Field | Value |
|---|---|
| Document type | Contributor Developer Guide |
| Audience | Backend engineers onboarding to or extending the platform |
| Companion docs | [ARCHITECTURE.md](ARCHITECTURE.md) · [HLD.md](HLD.md) · [LLD.md](LLD.md) · [CONTRIBUTING.md](../CONTRIBUTING.md) |
| Last revised | 2026-05-03 |

---

## Table of Contents

1. [Environment Setup](#1-environment-setup)
2. [Understanding the Codebase in 15 Minutes](#2-understanding-the-codebase-in-15-minutes)
3. [Adding a New Command (step-by-step)](#3-adding-a-new-command-step-by-step)
4. [Adding a New Query (step-by-step)](#4-adding-a-new-query-step-by-step)
5. [Adding a New Domain Event](#5-adding-a-new-domain-event)
6. [Adding a New Integration Event Consumer](#6-adding-a-new-integration-event-consumer)
7. [Adding a New Microservice (complete recipe)](#7-adding-a-new-microservice-complete-recipe)
8. [Working with the Saga](#8-working-with-the-saga)
9. [Writing Tests](#9-writing-tests)
10. [Working with the Database](#10-working-with-the-database)
11. [Caching Patterns](#11-caching-patterns)
12. [Multi-Tenancy Checklist](#12-multi-tenancy-checklist)
13. [Error Handling Patterns](#13-error-handling-patterns)
14. [Debugging and Observability Tools](#14-debugging-and-observability-tools)
15. [Common Mistakes and How to Avoid Them](#15-common-mistakes-and-how-to-avoid-them)
16. [Code Review Checklist](#16-code-review-checklist)
17. [Troubleshooting](#17-troubleshooting)

---

## 1. Environment Setup

### 1.1 Prerequisites

| Tool | Minimum version | Check |
|---|---|---|
| .NET SDK | 9.0 | `dotnet --version` |
| Docker Desktop | Latest | `docker --version` |
| Git | 2.40+ | `git --version` |
| IDE | VS 2022 17.12+ / Rider 2024.3+ / VS Code + C# Dev Kit | — |

### 1.2 First-time setup

```bash
git clone <repo>
cd "B2B Microservice Modern Architecture"

# Bring up the full infrastructure stack
docker compose up -d

# Verify all containers are healthy
docker compose ps

# Build the solution — must be 0 errors
dotnet build B2B.sln

# Run all tests — must be 586 passing, 0 failing
dotnet test B2B.sln
```

### 1.3 Running services locally

Open separate terminals for each service you need:

```bash
# Terminal 1 — Identity (required for JWT)
dotnet run --project src/Services/Identity/B2B.Identity.Api

# Terminal 2 — Product
dotnet run --project src/Services/Product/B2B.Product.Api

# Terminal 3 — Order
dotnet run --project src/Services/Order/B2B.Order.Api

# Terminal 4 — Gateway (routes all traffic)
dotnet run --project src/Gateway/B2B.Gateway
```

### 1.4 Local service URLs

| URL | Purpose |
|---|---|
| http://localhost:5000 | Gateway — use this for all API calls |
| http://localhost:5001/scalar | Identity Service Scalar UI |
| http://localhost:5002/scalar | Product Service Scalar UI |
| http://localhost:5003/scalar | Order Service Scalar UI |
| http://localhost:16686 | Jaeger — distributed traces |
| http://localhost:5341 | Seq — structured logs |
| http://localhost:3000 | Grafana — metrics dashboards (admin/admin) |
| http://localhost:9090 | Prometheus — raw metrics |
| http://localhost:15672 | RabbitMQ management (guest/guest) |
| http://localhost:8025 | MailHog — catch all outgoing email |
| http://localhost:5050 | pgAdmin — database browser |

### 1.5 Getting a JWT for local testing

```bash
# Register a tenant
curl -X POST http://localhost:5000/api/identity/tenants/register \
  -H "Content-Type: application/json" \
  -d '{"name":"ACME Corp","slug":"acme","adminEmail":"admin@acme.com","adminPassword":"P@ssw0rd!"}'

# Login to get a token
TOKEN=$(curl -s -X POST http://localhost:5000/api/identity/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@acme.com","password":"P@ssw0rd!"}' | jq -r '.accessToken')

# Use the token
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/orders
```

---

## 2. Understanding the Codebase in 15 Minutes

### 2.1 Where does a request go?

```
HTTP POST /api/orders
    │
    ▼  YARP Gateway (routes by path prefix, validates JWT)
    │
    ▼  Order.Api / OrdersController.cs
       await sender.Send(new CreateOrderCommand(...))
    │
    ▼  MediatR Pipeline (8 behaviors, see below)
    │
    ▼  CreateOrderHandler.Handle(command, ct)
       → call IOrderRepository, IUnitOfWork, etc.
       → order.Confirm() raises OrderConfirmedEvent
       → SaveChangesAsync commits
    │
    ▼  DomainEventBehavior publishes OrderConfirmedEvent (in-process)
    │
    ▼  WhenOrderConfirmed_PublishIntegration handler
       → IEventBus.PublishAsync(new OrderConfirmedIntegration(...))
    │
    ▼  MassTransitEventBus → RabbitMQ exchange
    │
    ▼  Notification Worker → email sent
```

### 2.2 The 8 pipeline behaviors (in order)

```
1. LoggingBehavior       — logs request name + total elapsed ms
2. RetryBehavior         — Bulkhead → Circuit Breaker → Retry (commands only)
3. IdempotencyBehavior   — 24h dedup via Redis (IIdempotentCommand only)
4. PerformanceBehavior   — warns on slow handlers
5. AuthorizationBehavior — role + resource-based access
6. ValidationBehavior    — FluentValidation; short-circuits on error
7. AuditBehavior         — writes audit record
8. DomainEventBehavior   — publishes domain events after SaveChangesAsync
   Handler
```

### 2.3 The Result pattern in 30 seconds

```csharp
// Handler returns Result<T> or Result — never throws business errors

// Success
return new CreateOrderResponse(order.Id, order.OrderNumber);

// Failure — implicit conversion to Result<TResponse>
return Error.NotFound("Order.NotFound", $"Order {id} not found.");
return Error.Validation("Order.Empty", "At least one item required.");
return Error.Conflict("Order.Duplicate", "Idempotency key already used.");

// Controller just calls ToActionResult() — no if/switch on error type
[HttpPost]
public async Task<IActionResult> Create(CreateOrderCommand cmd)
    => (await sender.Send(cmd)).ToActionResult();
```

### 2.4 Where is configuration?

| What | Where |
|---|---|
| NuGet package versions | `Directory.Packages.props` |
| Local infra env vars | `docker-compose.override.yml` |
| Service appsettings | `src/Services/{Svc}/{Svc}.Api/appsettings.json` |
| Gateway routes | `src/Gateway/B2B.Gateway/appsettings.json` |
| OTel Collector | `infrastructure/otel/collector.yaml` |
| Prometheus scrape | `infrastructure/prometheus/prometheus.yml` |

---

## 3. Adding a New Command (step-by-step)

### Example: `ArchiveOrderCommand`

#### Step 1 — Domain method

```csharp
// src/Services/Order/B2B.Order.Domain/Entities/Order.cs
public void Archive()
{
    if (CurrentStatus != OrderStatus.Delivered)
        throw new InvalidOperationException("Only delivered orders can be archived.");

    CurrentStatus = OrderStatus.Archived;
    RaiseDomainEvent(new OrderArchivedEvent(Id, TenantId, DateTime.UtcNow));
}
```

#### Step 2 — Domain event (if needed)

```csharp
// src/Services/Order/B2B.Order.Domain/Events/OrderArchivedEvent.cs
public record OrderArchivedEvent(Guid OrderId, Guid TenantId, DateTime ArchivedAt) : DomainEvent;
```

#### Step 3 — Command record

```csharp
// src/Services/Order/B2B.Order.Application/Commands/ArchiveOrder/ArchiveOrderCommand.cs
public sealed record ArchiveOrderCommand(Guid OrderId) : ICommand;
```

If the command affects money or should be idempotent, also implement `IIdempotentCommand`:

```csharp
public sealed record ArchiveOrderCommand(Guid OrderId, string IdempotencyKey)
    : ICommand, IIdempotentCommand;
```

#### Step 4 — Validator (optional but recommended)

```csharp
// src/Services/Order/B2B.Order.Application/Commands/ArchiveOrder/ArchiveOrderValidator.cs
public sealed class ArchiveOrderValidator : AbstractValidator<ArchiveOrderCommand>
{
    public ArchiveOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
```

Auto-discovered by `AddValidatorsFromAssembly` — no manual registration required.

#### Step 5 — Handler

```csharp
// src/Services/Order/B2B.Order.Application/Commands/ArchiveOrder/ArchiveOrderHandler.cs
using OrderEntity = B2B.Order.Domain.Entities.Order;  // ← type alias required

public sealed class ArchiveOrderHandler(
    IOrderRepository orderRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<ArchiveOrderCommand>
{
    public async Task<Result> Handle(ArchiveOrderCommand request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);

        if (order is null)
            return Error.NotFound("Order.NotFound", $"Order {request.OrderId} not found.");

        if (order.TenantId != currentUser.TenantId)
            return Error.Forbidden("Order.WrongTenant", "Access denied.");

        order.Archive();  // raises domain event internally

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

**Rules:**
- Inject only the ports you actually use
- Never reference `BCrypt.Net`, `StackExchange.Redis`, `Npgsql`, or `MassTransit` from Application layer
- Never throw business exceptions — return `Error.*`
- `TenantId` check is your safety net (global EF filter catches most cases, but explicit check on fetched entity is defence-in-depth)

#### Step 6 — Authorizer (resource-based access, if needed)

```csharp
// src/Services/Order/B2B.Order.Application/Commands/ArchiveOrder/ArchiveOrderAuthorizer.cs
public sealed class ArchiveOrderAuthorizer(IOrderRepository repo, ICurrentUser currentUser)
    : IAuthorizer<ArchiveOrderCommand>
{
    public async Task<AuthorizationResult> AuthorizeAsync(ArchiveOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(cmd.OrderId, ct);

        if (order is null || order.CustomerId != currentUser.UserId)
            return AuthorizationResult.Fail("You do not own this order.");

        if (!currentUser.IsInRole("buyer") && !currentUser.IsInRole("admin"))
            return AuthorizationResult.Fail("Role 'buyer' or 'admin' required.");

        return AuthorizationResult.Succeed();
    }
}
```

Register in the service's `DependencyInjection.cs`:

```csharp
services.AddScoped<IAuthorizer<ArchiveOrderCommand>, ArchiveOrderAuthorizer>();
```

#### Step 7 — Controller endpoint

```csharp
// src/Services/Order/B2B.Order.Api/Controllers/OrdersController.cs
[HttpPost("{id:guid}/archive")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public async Task<IActionResult> Archive(Guid id)
    => (await sender.Send(new ArchiveOrderCommand(id))).ToActionResult();
```

#### Step 8 — Tests

See [§9](#9-writing-tests) for test patterns.

---

## 4. Adding a New Query (step-by-step)

### Example: `GetOrderStatsQuery`

#### Step 1 — Response DTO

```csharp
// src/Services/Order/B2B.Order.Application/Queries/GetOrderStats/OrderStatsDto.cs
public sealed record OrderStatsDto(
    int TotalOrders,
    int PendingOrders,
    decimal TotalRevenue);
```

#### Step 2 — Query record

```csharp
// src/Services/Order/B2B.Order.Application/Queries/GetOrderStats/GetOrderStatsQuery.cs
public sealed record GetOrderStatsQuery(DateOnly From, DateOnly To) : IQuery<OrderStatsDto>;
```

#### Step 3 — Handler with HybridCache

```csharp
// src/Services/Order/B2B.Order.Application/Queries/GetOrderStats/GetOrderStatsHandler.cs
using Microsoft.Extensions.Caching.Hybrid;

public sealed class GetOrderStatsHandler(
    IReadOrderRepository repo,
    ICurrentUser currentUser,
    HybridCache hybridCache) : IQueryHandler<GetOrderStatsQuery, OrderStatsDto>
{
    public async Task<Result<OrderStatsDto>> Handle(GetOrderStatsQuery q, CancellationToken ct)
    {
        var key = $"order-stats:tenant:{currentUser.TenantId}:{q.From}:{q.To}";

        var stats = await hybridCache.GetOrCreateAsync(
            key,
            async cancel => await repo.GetStatsAsync(currentUser.TenantId, q.From, q.To, cancel),
            cancellationToken: ct);

        return stats;
    }
}
```

**When to use HybridCache vs ICacheService:**
- Use `HybridCache` for query handler results — stampede-safe, L1+L2 automatic
- Use `ICacheService` when you need prefix-based invalidation or custom TTLs in command handlers

#### Step 4 — Repository method

```csharp
// In IReadOrderRepository (Application/Interfaces)
Task<OrderStatsDto> GetStatsAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct);

// In OrderReadRepository (Infrastructure)
public async Task<OrderStatsDto> GetStatsAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct)
{
    await using var ctx = await _factory.CreateDbContextAsync(ct);

    // Note: read repository uses IDbContextFactory (NoTracking, read replica)
    // Global tenant filter NOT applied here (read repo bypasses it) — filter manually
    var query = ctx.Orders
        .Where(o => o.TenantId == tenantId &&
                    o.CreatedAt >= from.ToDateTime(TimeOnly.MinValue) &&
                    o.CreatedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue));

    return new OrderStatsDto(
        TotalOrders:   await query.CountAsync(ct),
        PendingOrders: await query.CountAsync(o => o.Status == OrderStatus.Pending, ct),
        TotalRevenue:  await query.SumAsync(o => o.TotalAmount, ct));
}
```

> **Note:** Read repositories using `IDbContextFactory` create contexts via `CreateDbContextAsync`, not DI scopes. The global tenant filter is applied to scoped DI contexts but **not** factory-created contexts — always filter explicitly in read repositories.

#### Step 5 — Controller

```csharp
[HttpGet("stats")]
[OutputCache(PolicyName = "queries")]   // 30s cache, vary by query params
[ProducesResponseType(typeof(OrderStatsDto), StatusCodes.Status200OK)]
public async Task<IActionResult> GetStats([FromQuery] GetOrderStatsQuery query)
    => (await sender.Send(query)).ToActionResult();
```

---

## 5. Adding a New Domain Event

### Definition

```csharp
// In {Svc}.Domain/Events/
public record OrderArchivedEvent(Guid OrderId, Guid TenantId, DateTime ArchivedAt) : DomainEvent;
```

### Raising in aggregate

```csharp
// Inside AggregateRoot method — never raise from outside the aggregate
public void Archive()
{
    // ... validate state ...
    RaiseDomainEvent(new OrderArchivedEvent(Id, TenantId, DateTime.UtcNow));
}
```

### Handling — convert to integration event

```csharp
// In {Svc}.Application or {Svc}.Infrastructure
public sealed class WhenOrderArchived_PublishIntegration(IEventBus bus)
    : INotificationHandler<OrderArchivedEvent>
{
    public async Task Handle(OrderArchivedEvent e, CancellationToken ct) =>
        await bus.PublishAsync(new OrderArchivedIntegration(e.OrderId, e.TenantId, e.ArchivedAt), ct);
}
```

Auto-discovered by MediatR's assembly scan — no manual registration.

### Handling — cache invalidation

```csharp
public sealed class WhenOrderArchived_InvalidateCache(ICacheService cache)
    : INotificationHandler<OrderArchivedEvent>
{
    public async Task Handle(OrderArchivedEvent e, CancellationToken ct) =>
        await cache.RemoveByPrefixAsync($"orders:tenant:{e.TenantId}");
}
```

Multiple handlers for the same event are all called — add as many as needed.

### Add integration event contract (if cross-service)

```csharp
// B2B.Shared.Core/Messaging/IntegrationEvents.cs — append to the file
public sealed record OrderArchivedIntegration(Guid OrderId, Guid TenantId, DateTime ArchivedAt);
```

---

## 6. Adding a New Integration Event Consumer

### In a service (e.g. react to OrderArchivedIntegration in the Notification Worker)

```csharp
// workers/B2B.Notification.Worker/Consumers/OrderArchivedConsumer.cs
public sealed class OrderArchivedConsumer(IEmailService emailService, ILogger<OrderArchivedConsumer> logger)
    : IConsumer<OrderArchivedIntegration>
{
    public async Task Consume(ConsumeContext<OrderArchivedIntegration> context)
    {
        var msg = context.Message;
        logger.LogInformation("Order {OrderId} archived — notifying tenant {TenantId}", msg.OrderId, msg.TenantId);

        await emailService.SendAsync(new EmailMessage(
            To: "admin@tenant.com",
            Subject: $"Order {msg.OrderId} archived",
            Body: $"Order was archived at {msg.ArchivedAt:u}"));
    }
}
```

Register in `Program.cs` (or the service's `ConfigureOrderBusParticipants` equivalent):

```csharp
x.AddConsumer<OrderArchivedConsumer>();
```

MassTransit automatically creates a queue named after the consumer + exchange named after the message contract. `cfg.ConfigureEndpoints(ctx)` wires everything.

---

## 7. Adding a New Microservice (complete recipe)

### Example: `B2B.Analytics` service

#### Step 1 — Create four projects

```
src/Services/Analytics/
├── B2B.Analytics.Domain/
├── B2B.Analytics.Application/
├── B2B.Analytics.Infrastructure/
└── B2B.Analytics.Api/
```

In each `.csproj`, follow the dependency rule:

```xml
<!-- B2B.Analytics.Domain.csproj -->
<PackageReference Include="B2B.Shared.Core" />  <!-- only Shared.Core -->

<!-- B2B.Analytics.Application.csproj -->
<ProjectReference Include="../B2B.Analytics.Domain/..." />
<!-- B2B.Shared.Core referenced transitively via Domain -->

<!-- B2B.Analytics.Infrastructure.csproj -->
<ProjectReference Include="../B2B.Analytics.Application/..." />
<PackageReference Include="B2B.Shared.Infrastructure" />

<!-- B2B.Analytics.Api.csproj -->
<ProjectReference Include="../B2B.Analytics.Infrastructure/..." />
```

#### Step 2 — Domain entities

```csharp
// B2B.Analytics.Domain/Entities/AnalyticsEvent.cs
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

public class AnalyticsEvent : AggregateRoot<Guid>, IAuditableEntity, ITenantEntity
{
    public Guid TenantId { get; private set; }  // ← ITenantEntity — automatic filter applied
    public string EventType { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    protected AnalyticsEvent() { }

    public static AnalyticsEvent Create(Guid tenantId, string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        var entity = new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        entity.RaiseDomainEvent(new AnalyticsEventCreated(entity.Id, tenantId, eventType));
        return entity;
    }
}
```

#### Step 3 — Application interfaces

```csharp
// B2B.Analytics.Application/Interfaces/IAnalyticsRepository.cs
public interface IAnalyticsRepository : IRepository<AnalyticsEvent, Guid>
{
    Task<PagedList<AnalyticsEvent>> GetByTenantAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct);
}
```

#### Step 4 — Infrastructure — DbContext

```csharp
// B2B.Analytics.Infrastructure/Persistence/AnalyticsDbContext.cs
public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options, IServiceProvider serviceProvider)
    : BaseDbContext(options, serviceProvider), IUnitOfWork
{
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await base.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        await using var tx = await Database.BeginTransactionAsync(ct);
        await action();
        await SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // ← applies global tenant filter
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalyticsDbContext).Assembly);
    }
}
```

#### Step 5 — Infrastructure — DependencyInjection.cs

```csharp
// B2B.Analytics.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddPostgres<AnalyticsDbContext>(config);
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AnalyticsDbContext>());
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        return services;
    }
}
```

#### Step 6 — API Program.cs

```csharp
// B2B.Analytics.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSharedInfrastructure(
        builder.Configuration,
        "B2B.Analytics",
        new[] { typeof(AnalyticsAssemblyMarker).Assembly })
    .AddAnalyticsInfrastructure(builder.Configuration)
    .AddEventBus(builder.Configuration, x =>
    {
        x.AddConsumer<SomeConsumer>();
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
app.UseSharedMiddleware();
app.MapControllers();
app.Run();
```

#### Step 7 — YARP route

```json
// src/Gateway/B2B.Gateway/appsettings.json
"Routes": {
  "analytics-route": {
    "ClusterId": "analytics-cluster",
    "Match": { "Path": "/api/analytics/{**catch-all}" },
    "Transforms": [{ "PathRemovePrefix": "/api/analytics" }],
    "AuthorizationPolicy": "default"
  }
},
"Clusters": {
  "analytics-cluster": {
    "Destinations": {
      "analytics-primary": { "Address": "http://analytics-service:8080" }
    },
    "HealthCheck": {
      "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }
    }
  }
}
```

#### Step 8 — docker-compose.yml

```yaml
# docker-compose.yml — add to services section
analytics-service:
  build:
    context: .
    dockerfile: src/Services/Analytics/B2B.Analytics.Api/Dockerfile
  depends_on: [postgres, redis, rabbitmq, otel-collector]
  networks: [b2b-network]
  ports: ["5012:8080"]
```

```yaml
# docker-compose.override.yml — add environment variables
analytics-service:
  environment:
    - ConnectionStrings__DefaultConnection=Host=pgbouncer;Port=6432;Database=b2b_analytics;...
    - ConnectionStrings__Redis=redis:6379
    - RabbitMQ__Host=rabbitmq
    - JwtSettings__SecretKey=${JWT_SECRET}
    - OpenTelemetry__Endpoint=http://otel-collector:4317
```

#### Step 9 — Database

```sql
-- infrastructure/postgres/init.sql — append
CREATE DATABASE b2b_analytics;
GRANT ALL PRIVILEGES ON DATABASE b2b_analytics TO b2b_user;
```

#### Step 10 — Test project

```
tests/B2B.Analytics.Tests/
├── B2B.Analytics.Tests.csproj
├── Domain/
│   └── AnalyticsEventTests.cs
└── Application/
    └── Commands/
        └── CreateAnalyticsEventHandlerTests.cs
```

Register in `B2B.sln`:

```bash
dotnet sln add tests/B2B.Analytics.Tests/B2B.Analytics.Tests.csproj
```

---

## 8. Working with the Saga

### 8.1 Saga state and options

```csharp
// OrderFulfillmentSagaState — the EF-persisted row
public class OrderFulfillmentSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime InitiatedAt { get; set; }
    // ... other fields
}
```

Configure timeouts in `appsettings.json`:

```json
"OrderFulfillmentSaga": {
  "StockReservationDeadline": "00:05:00",
  "PaymentDeadline": "00:10:00",
  "ShipmentDeadline": "02:00:00"
}
```

### 8.2 Triggering the saga

The saga starts when `OrderFulfillmentSaga` receives `OrderConfirmedIntegration`. This is published by `WhenOrderConfirmed_PublishIntegration` domain event handler inside the Order service after `SaveChangesAsync`.

### 8.3 Adding a new saga step

If you need to insert a step (e.g., a fraud check between stock and payment):

1. Add a new state: `AwaitingFraudCheck`
2. Add the state machine transition in `OrderFulfillmentSaga`
3. Add compensation logic: what to do if fraud check fails
4. Add a timeout for the new step in `OrderFulfillmentSagaOptions`
5. Add the consumer for the fraud check reply
6. Update `StuckSagaCleanupWorker` to include the new state and its timeout threshold

### 8.4 Inspecting saga state

```bash
# In pgAdmin or psql
SELECT correlation_id, order_id, tenant_id, current_state, initiated_at
FROM order_fulfillment_saga_states
WHERE current_state NOT IN ('Final')
ORDER BY initiated_at;
```

---

## 9. Writing Tests

### 9.1 Handler test pattern

```csharp
// tests/B2B.Order.Tests/Application/Commands/ArchiveOrderHandlerTests.cs
using OrderEntity = B2B.Order.Domain.Entities.Order;  // ← type alias

public sealed class ArchiveOrderHandlerTests
{
    // ── Mocks ──────────────────────────────────────────────────────────────
    private readonly IOrderRepository _repo  = Substitute.For<IOrderRepository>();
    private readonly ICurrentUser _user      = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow        = Substitute.For<IUnitOfWork>();
    private readonly ArchiveOrderHandler _handler;

    // ── Fixture data ────────────────────────────────────────────────────────
    private readonly Guid _tenantId  = Guid.NewGuid();
    private readonly Guid _userId    = Guid.NewGuid();
    private readonly Guid _orderId   = Guid.NewGuid();

    public ArchiveOrderHandlerTests()
    {
        _user.TenantId.Returns(_tenantId);
        _user.UserId.Returns(_userId);

        _handler = new ArchiveOrderHandler(_repo, _user, _uow);
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _repo.GetByIdAsync(_orderId, Arg.Any<CancellationToken>())
             .Returns((OrderEntity?)null);

        // Act
        var result = await _handler.Handle(new ArchiveOrderCommand(_orderId), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOrderBelongsToWrongTenant_ShouldReturnForbidden()
    {
        // Arrange
        var order = CreateDeliveredOrder(tenantId: Guid.NewGuid()); // different tenant
        _repo.GetByIdAsync(_orderId, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _handler.Handle(new ArchiveOrderCommand(_orderId), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenOrderDelivered_ShouldArchiveAndSave()
    {
        // Arrange
        var order = CreateDeliveredOrder(tenantId: _tenantId);
        _repo.GetByIdAsync(_orderId, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _handler.Handle(new ArchiveOrderCommand(_orderId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.CurrentStatus.Should().Be(OrderStatus.Archived);
        order.DomainEvents.Should().ContainSingle(e => e is OrderArchivedEvent);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private OrderEntity CreateDeliveredOrder(Guid tenantId) =>
        // Use domain factory — never set properties directly via reflection
        OrderEntity.CreateForTest(id: _orderId, tenantId: tenantId, status: OrderStatus.Delivered);
}
```

### 9.2 Domain test pattern

```csharp
public sealed class OrderTests
{
    [Fact]
    public void Archive_WhenDelivered_ShouldRaiseEvent()
    {
        // Arrange
        var order = OrderEntity.Create(...);
        order.Ship("TRACK123");
        order.Deliver();  // put into Delivered state

        // Act
        order.Archive();

        // Assert
        order.CurrentStatus.Should().Be(OrderStatus.Archived);
        order.DomainEvents.Should().ContainSingle(e => e is OrderArchivedEvent);
    }

    [Fact]
    public void Archive_WhenNotDelivered_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var order = OrderEntity.Create(...);  // status: Pending

        // Act
        var act = () => order.Archive();

        // Assert — domain invariants throw, not Result failure
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Only delivered orders can be archived.");
    }
}
```

### 9.3 Validator test pattern

```csharp
public sealed class ArchiveOrderValidatorTests
{
    private readonly ArchiveOrderValidator _validator = new();

    [Fact]
    public void Validate_Valid_ShouldPass()
    {
        _validator.TestValidate(new ArchiveOrderCommand(Guid.NewGuid()))
                  .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyOrderId_ShouldFail()
    {
        _validator.TestValidate(new ArchiveOrderCommand(Guid.Empty))
                  .ShouldHaveValidationErrorFor(x => x.OrderId);
    }
}
```

### 9.4 Authorizer test pattern

```csharp
public sealed class ArchiveOrderAuthorizerTests
{
    private readonly IOrderRepository _repo = Substitute.For<IOrderRepository>();
    private readonly ICurrentUser _user     = Substitute.For<ICurrentUser>();
    private readonly ArchiveOrderAuthorizer _authorizer;

    private readonly Guid _userId   = Guid.NewGuid();
    private readonly Guid _orderId  = Guid.NewGuid();

    public ArchiveOrderAuthorizerTests()
    {
        _user.UserId.Returns(_userId);
        _user.IsInRole("buyer").Returns(true);
        _authorizer = new ArchiveOrderAuthorizer(_repo, _user);
    }

    [Fact]
    public async Task Authorize_WhenOwnerAndBuyer_ShouldSucceed()
    {
        _repo.GetByIdAsync(_orderId, Arg.Any<CancellationToken>())
             .Returns(OrderEntity.CreateForTest(customerId: _userId));

        var result = await _authorizer.AuthorizeAsync(
            new ArchiveOrderCommand(_orderId), CancellationToken.None);

        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task Authorize_WhenNotOwner_ShouldFail()
    {
        _repo.GetByIdAsync(_orderId, Arg.Any<CancellationToken>())
             .Returns(OrderEntity.CreateForTest(customerId: Guid.NewGuid())); // different user

        var result = await _authorizer.AuthorizeAsync(
            new ArchiveOrderCommand(_orderId), CancellationToken.None);

        result.IsAuthorized.Should().BeFalse();
    }
}
```

### 9.5 Running tests

```bash
# All tests
dotnet test B2B.sln

# Single project
dotnet test tests/B2B.Order.Tests/B2B.Order.Tests.csproj -v normal

# Single test class
dotnet test --filter "FullyQualifiedName~ArchiveOrderHandlerTests"

# Single test method
dotnet test --filter "FullyQualifiedName~ArchiveOrderHandlerTests.Handle_WhenOrderNotFound"

# With coverage (requires coverlet)
dotnet test B2B.sln --collect:"XPlat Code Coverage"
```

---

## 10. Working with the Database

### 10.1 Adding a migration

```bash
# From the service's Infrastructure project directory
cd src/Services/Order/B2B.Order.Infrastructure

dotnet ef migrations add AddOrderArchivedAt \
    --startup-project ../B2B.Order.Api \
    --output-dir Persistence/Migrations

dotnet ef database update \
    --startup-project ../B2B.Order.Api
```

### 10.2 EF entity configuration

```csharp
// B2B.Order.Infrastructure/Persistence/Configurations/OrderConfiguration.cs
public class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.HasKey(o => o.Id);

        // Never configure TenantId HasQueryFilter here — BaseDbContext does it automatically
        builder.Property(o => o.TenantId).IsRequired();

        builder.Property(o => o.OrderNumber)
               .IsRequired()
               .HasMaxLength(FieldLengths.OrderNumber);  // use shared constants

        builder.Property(o => o.Status)
               .HasConversion<string>()                  // store as string, not int
               .HasMaxLength(50);

        builder.HasMany(o => o.Items)
               .WithOne()
               .HasForeignKey("OrderId")
               .OnDelete(DeleteBehavior.Cascade);

        // RowVersion for optimistic concurrency (roadmap — not yet applied)
        // builder.Property<uint>("RowVersion").IsRowVersion();
    }
}
```

### 10.3 FieldLengths constants

All database field lengths are defined in `B2B.Shared.Core/Common/FieldLengths.cs`. Use these constants in EF configurations and domain factories:

```csharp
public static class FieldLengths
{
    public const int Name     = 200;
    public const int Email    = 256;
    public const int Slug     = 100;
    public const int OrderNumber = 50;
    // etc.
}
```

### 10.4 Read repository pattern

```csharp
// Read repositories use IDbContextFactory — NoTracking, read replica
public sealed class OrderReadRepository(IDbContextFactory<OrderDbContext> factory)
    : IReadOrderRepository
{
    public async Task<OrderEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        // ⚠️ Global tenant filter NOT applied — factory contexts bypass it
        // Always filter manually:
        return await ctx.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }
}
```

### 10.5 Checking connection string targets PgBouncer

```
# Connection string format for PgBouncer transaction mode
Host=pgbouncer;Port=6432;Database=b2b_order;Username=b2b_user;Password=secret;
No Reset On Close=true;Pooling=false
```

`No Reset On Close=true` prevents Npgsql from sending `RESET ALL` on connection return — transaction-mode PgBouncer doesn't hold connections between statements so reset commands fail.

---

## 11. Caching Patterns

### 11.1 Cache keys — naming convention

```
{resource}:tenant:{tenantId}:{qualifier}:{value}

Examples:
  products:tenant:{tenantId}:page:{page}:size:{pageSize}
  orders:tenant:{tenantId}:customer:{customerId}:page:{page}
  order-stats:tenant:{tenantId}:{from}:{to}
  idem:B2B.Order.Application.Commands.CreateOrderCommand:{idempotencyKey}
```

Always include `tenantId` in the key. Never share a cache key between tenants.

### 11.2 Invalidation strategy

```csharp
// In command handler AFTER SaveChangesAsync:
await cache.RemoveByPrefixAsync($"products:tenant:{currentUser.TenantId}");

// Or in domain event handler:
public sealed class WhenProductUpdated_InvalidateCache(ICacheService cache)
    : INotificationHandler<ProductUpdatedEvent>
{
    public async Task Handle(ProductUpdatedEvent e, CancellationToken ct) =>
        await cache.RemoveByPrefixAsync($"products:tenant:{e.TenantId}");
}
```

### 11.3 What NOT to cache

- Write-side repository results (command handlers should read fresh data)
- Results from `IUnitOfWork.SaveChangesAsync` — the return value is the saved entity count, not cached
- Anything containing PII without explicit security review
- Saga state — MassTransit manages this

---

## 12. Multi-Tenancy Checklist

When adding a new entity or feature, verify:

- [ ] Entity implements `ITenantEntity` (if tenant-scoped)
- [ ] Entity's `TenantId` is set in the domain factory from `ICurrentUser.TenantId`
- [ ] Read repositories that use `IDbContextFactory` (not DI scope) filter by `tenantId` manually
- [ ] Background workers that scan across tenants call `IgnoreQueryFilters()` explicitly and document why
- [ ] Cache keys include `tenantId` — no cross-tenant key collisions
- [ ] Integration events include `TenantId` in the contract
- [ ] Tests set `ICurrentUser.TenantId.Returns(...)` to a specific value

---

## 13. Error Handling Patterns

### 13.1 Business errors (return, don't throw)

```csharp
// ✅ Correct — business failure as a value
if (order is null)
    return Error.NotFound("Order.NotFound", $"Order {id} not found.");

// ❌ Wrong — throwing for a predictable business condition
if (order is null)
    throw new OrderNotFoundException(id);
```

### 13.2 Domain invariants (throw, don't return)

```csharp
// ✅ Correct — invariants enforced in the aggregate, not the handler
public void Cancel(string reason)
{
    if (CurrentStatus == OrderStatus.Delivered)
        throw new InvalidOperationException("Cannot cancel a delivered order.");
    // ...
}

// Handler catches nothing — if Archive() throws, it's a bug, not a business error
order.Archive();
```

### 13.3 HTTP 503 — service unavailable

`Error.ServiceUnavailable` is returned by `RetryBehavior` when:
- Bulkhead is saturated (`BulkheadMaxConcurrency` concurrent calls of the same command type)
- Circuit breaker is in Open state (failure ratio exceeded)

Clients receiving 503 should implement exponential backoff. The `Retry-After` header is not currently set — this is on the roadmap.

### 13.4 Error code conventions

```
{Service}.{Entity}.{Condition}

Examples:
  "Order.NotFound"
  "Order.AlreadyCancelled"
  "Product.SkuExists"
  "Vendor.TaxIdDuplicate"
  "Auth.Forbidden"
  "Bulkhead.Full"
  "CircuitBreaker.Open"
```

---

## 14. Debugging and Observability Tools

### 14.1 Tracing a request end-to-end

1. Make a request through the gateway
2. Look at the `traceparent` response header — copy the trace ID
3. Open Jaeger: http://localhost:16686
4. Search by trace ID → see every span: Gateway → Service → DB query → RabbitMQ publish → Notification Worker

### 14.2 Finding errors in Seq

```
# Seq query syntax
Level = 'Error' AND RequestName = 'CreateOrderCommand'
Level = 'Warning' AND @Message LIKE '%stuck saga%'
UserId = '...'
TenantId = '...'
```

Every request is tagged with `RequestName`, `UserId`, `TenantId`, `TraceId`.

### 14.3 Watching RabbitMQ queues

1. Open http://localhost:15672 (guest/guest)
2. Go to **Queues** tab
3. Look for queue depth — consumers should drain quickly
4. `Ready` count rising = consumers not keeping up or consumer crashed

### 14.4 Checking Grafana dashboards

1. Open http://localhost:3000 (admin/admin)
2. Pre-provisioned Prometheus datasource
3. Look for:
   - `dotnet_gc_duration_seconds` — GC pressure
   - `dotnet_threadpool_threads_count` — thread pool usage
   - `http_server_request_duration_seconds` — request latency
   - `http_server_active_requests` — concurrency

### 14.5 Common log queries for debugging

```
# Circuit breaker transitions
@Message LIKE '%Circuit breaker%'

# Bulkhead saturation
@Message LIKE '%Bulkhead saturated%'

# Stuck saga detections
@Message LIKE '%Stuck saga%'

# Slow handlers
@Message LIKE '%Handled%' AND Elapsed > 500

# Retry attempts
@Message LIKE '%Transient failure%'
```

---

## 15. Common Mistakes and How to Avoid Them

### 15.1 Forgetting `ITenantEntity` on a new entity

**Symptom:** Cross-tenant data visible; one tenant can see another's records.

**Fix:** Implement `ITenantEntity` on the entity. The global filter is applied automatically.

```csharp
// ❌ Missing ITenantEntity
public class Report : AggregateRoot<Guid>, IAuditableEntity { }

// ✅ Correct
public class Report : AggregateRoot<Guid>, IAuditableEntity, ITenantEntity
{
    public Guid TenantId { get; private set; }
}
```

### 15.2 Missing type alias causing CS0118

**Symptom:** `CS0118: 'Order' is a namespace but is used like a type`

**Fix:** Add type alias at the top of the file:

```csharp
using OrderEntity     = B2B.Order.Domain.Entities.Order;
using OrderItemEntity = B2B.Order.Domain.Entities.OrderItem;
using OrderStatus     = B2B.Order.Domain.Entities.OrderStatus;
```

Required in all files under `B2B.Order.*`, `B2B.Product.*`, `B2B.Vendor.*`.

### 15.3 Wrong MediatR 12 delegate signature

**Symptom:** `CS1501: No overload for method 'next' takes 1 arguments`

**Fix:** `RequestHandlerDelegate<TResponse>` takes **no parameters** in MediatR 12:

```csharp
// ❌ Wrong
var response = await next(cancellationToken);

// ✅ Correct
var response = await next();
```

### 15.4 Calling SaveChangesAsync before raising domain events

**Symptom:** Domain events never fire; notifications not sent.

**Fix:** Domain events are drained by `DomainEventBehavior` **after** the handler returns. The handler just calls the aggregate method and `SaveChangesAsync` — the pipeline handles event publishing.

```csharp
// ✅ Correct handler pattern
order.Confirm();             // raises OrderConfirmedEvent internally
await uow.SaveChangesAsync(ct);  // commits; DomainEventBehavior publishes event after this
return new CreateOrderResponse(order.Id);
// ← DomainEventBehavior runs here, AFTER return
```

### 15.5 Returning exceptions instead of Result failures

**Symptom:** 500 Internal Server Error for predictable business conditions.

**Fix:** Return `Error.*` instead of throwing:

```csharp
// ❌ Wrong
throw new NotFoundException($"Order {id} not found");

// ✅ Correct
return Error.NotFound("Order.NotFound", $"Order {id} not found");
```

### 15.6 Filtering in read repositories without tenantId

**Symptom:** Cross-tenant data leak in query handlers; read repos don't get the global filter.

**Fix:** Read repositories use `IDbContextFactory` — the global EF filter is **not** applied to factory-created contexts. Always filter manually:

```csharp
// ❌ Missing tenant filter in read repository
var orders = await ctx.Orders.ToListAsync(ct);  // returns ALL tenants!

// ✅ Correct
var orders = await ctx.Orders
    .Where(o => o.TenantId == tenantId)
    .ToListAsync(ct);
```

### 15.7 Adding a NuGet package without pinning the version

**Symptom:** NU1903 warning or version conflict.

**Fix:** All package versions are centrally managed in `Directory.Packages.props`. Add the version entry there; reference the package without a version in `.csproj`.

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />

<!-- *.csproj -->
<PackageReference Include="Newtonsoft.Json" />  <!-- no Version attribute -->
```

### 15.8 Forgetting PgBouncer connection string options

**Symptom:** Npgsql errors like `protocol violation` or `prepared statement already exists`

**Fix:** Add `No Reset On Close=true` to the connection string when pointing at PgBouncer (transaction mode):

```
Host=pgbouncer;Port=6432;Database=b2b_order;...;No Reset On Close=true
```

---

## 16. Code Review Checklist

Reviewers verify the following before approving any PR:

### Architecture

- [ ] Dependency rule respected — no reverse references (Domain → Infrastructure, Application → Infrastructure, etc.)
- [ ] No business logic in controllers — handlers only
- [ ] No direct use of `BCrypt.Net`, `StackExchange.Redis`, `Npgsql`, or `MassTransit` from Application or Domain layers
- [ ] Handler injects only the ports it actually needs

### Multi-tenancy

- [ ] New entity implements `ITenantEntity` if tenant-scoped
- [ ] `TenantId` set in domain factory from `ICurrentUser`
- [ ] Read repositories using `IDbContextFactory` filter by `tenantId` explicitly
- [ ] Background services that scan all tenants call `IgnoreQueryFilters()` explicitly
- [ ] Cache keys include `tenantId`

### Result pattern

- [ ] No business exceptions thrown — all failures returned as `Error.*`
- [ ] `Error.*` factory method used (not raw `new Error(...)`)
- [ ] Controller method calls `ToActionResult()` — no manual status code branching

### CQRS

- [ ] Commands return `Result` or `Result<T>` — never raw data
- [ ] Queries have no side effects — no mutations, no `SaveChangesAsync`
- [ ] `IIdempotentCommand` implemented on money-affecting commands

### Testing

- [ ] Success path tested
- [ ] Every `Error.*` branch tested
- [ ] Authorizer tests cover all success and failure paths
- [ ] Validator tests cover all rules and boundary values
- [ ] `SaveChangesAsync` called `Received(1)` on success, `DidNotReceive` on failure

### Type aliases

- [ ] Files under `B2B.Order.*`, `B2B.Product.*`, `B2B.Vendor.*` use the required type aliases

### Documentation

- [ ] Public API change reflected in HLD/LLD/BRD if architectural
- [ ] New `IOptions<>` section documented in configuration reference

---

## 17. Troubleshooting

| Symptom | Check | Fix |
|---|---|---|
| `CS0118: 'Order' is a namespace` | Missing type alias | Add `using OrderEntity = B2B.Order.Domain.Entities.Order;` |
| `CS1501: next takes 1 argument` | MediatR 12 signature | Change `await next(ct)` to `await next()` |
| `JWT 401` from gateway | Issuer/audience mismatch | Verify `JwtSettings:Issuer` matches between Identity and services |
| Domain events not firing | Handler not using tracked entity | Ensure entity is fetched via scoped `DbContext`, not `IDbContextFactory` |
| Cross-tenant data returned | Missing `ITenantEntity` or read repo not filtering | Implement `ITenantEntity`; add `.Where(o => o.TenantId == tenantId)` to read repo |
| 503 from command endpoint | Bulkhead saturated or circuit open | Check Seq for `Bulkhead saturated` or `Circuit breaker OPEN` log entries |
| `NU1903` NuGet warning | Package without version pin | Add version to `Directory.Packages.props` |
| Saga stuck in AwaitingStock | RabbitMQ delayed scheduler not running | Verify `UseDelayedMessageScheduler()` in Order bus config; check RabbitMQ delayed exchange plugin |
| Basket 404 after restart | Redis restart — expected | Basket is intentionally ephemeral (BRD FR-BS-7) |
| `protocol violation` on PgBouncer | Missing `No Reset On Close=true` | Add flag to connection string |
| Grafana shows no metrics | OTel Collector not receiving | Check `otel-collector` container logs; verify `ASPNETCORE_URLS` port matches `OpenTelemetry:Endpoint` |
| `BrokenCircuitException` in logs | Circuit breaker opened | Underlying dependency failure; check DB/Redis/RabbitMQ health; circuit auto-recovers in 30s |
| HybridCache returning stale data | L1 TTL not expired | L1 expires in 2 min; force expiry with `cache.RemoveAsync(key)` |

---

*Document type: Contributor Developer Guide*
*Platform: B2B Microservice Modern Architecture*
*Last revised: 2026-05-03*
