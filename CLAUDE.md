# B2B Microservice Modern Architecture

Production-grade B2B microservice template using ASP.NET Core 9, Clean Architecture, CQRS, DDD, and Event-Driven messaging.

## Solution Structure

```
src/
  Gateway/
    B2B.Gateway/                   # YARP reverse proxy, rate limiting, JWT validation
  Shared/
    B2B.Shared.Core/               # Domain abstractions, CQRS interfaces, Result pattern
    B2B.Shared.Infrastructure/     # EF Core base, MassTransit, Redis, behaviors
  Services/
    Identity/
      B2B.Identity.Domain/
      B2B.Identity.Application/
      B2B.Identity.Infrastructure/
      B2B.Identity.Api/            # :5001
    Product/
      B2B.Product.Domain/
      B2B.Product.Application/
      B2B.Product.Infrastructure/
      B2B.Product.Api/             # :5002
    Order/
      B2B.Order.Domain/
      B2B.Order.Application/
      B2B.Order.Infrastructure/
      B2B.Order.Api/               # :5003
  Workers/
    B2B.Notification.Worker/       # MassTransit consumers, SMTP email
tests/
  B2B.Product.Tests/               # 13 unit tests (xUnit + FluentAssertions)
  B2B.Order.Tests/                 # 7 unit tests
infrastructure/
  postgres/init.sql                # Creates b2b_identity, b2b_product, b2b_order databases
```

## SOLID Principles

Every layer of this solution is designed around SOLID. The table below maps each principle to concrete examples in the codebase.

### S — Single Responsibility

Each class has exactly one reason to change.

| Class | Single Responsibility |
|---|---|
| `CreateOrderHandler` | Orchestrates order creation only |
| `ValidationBehavior` | Input validation only |
| `BcryptPasswordHasher` | Password hashing only |
| `RedisCacheService` | Cache read/write only |
| `SmtpEmailService` | Email delivery only |
| `TokenService` | JWT generation/validation only |

```csharp
// Handler does ONE thing — creates an order. Validation, logging, and event
// publishing are handled by separate pipeline behaviors.
public sealed class CreateOrderHandler(
    IOrderRepository orderRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public async Task<Result<CreateOrderResponse>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // only order-creation logic here
    }
}
```

### O — Open/Closed

Open for extension, closed for modification. New cross-cutting concerns are added as new pipeline behaviors — existing handlers are never touched.

```csharp
// Add a new behavior (e.g. idempotency, retry) without modifying any handler
public sealed class IdempotencyBehavior<TRequest, TResponse>(ICacheService cache)
    : IPipelineBehavior<TRequest, TResponse> { ... }

// Register in DI — handlers are unaware
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
```

Domain entities use factory methods so internal invariants are enforced without subclassing:

```csharp
// New product types extend behavior via domain events, not inheritance
public static Product Create(...) { /* enforces invariants */ }
```

### L — Liskov Substitution

Any implementation of an interface can substitute the abstraction without breaking callers.

```csharp
// Handlers depend on IOrderRepository — works with EF Core repo, in-memory stub, or any future impl
public interface IOrderRepository : IRepository<OrderEntity, Guid> { ... }

// Swap cache backends transparently
public interface ICacheService { ... }          // callers never know Redis vs Memory
public class RedisCacheService : ICacheService  // production
public class MemoryCacheService : ICacheService // tests
```

### I — Interface Segregation

No interface forces its clients to depend on methods they do not use. Each interface is narrow and role-specific.

```csharp
IRepository<T, TId>   // persistence (GetById, Add, Update, Remove)
IUnitOfWork           // transaction boundary (SaveChangesAsync, ExecuteInTransactionAsync)
ICacheService         // caching (Get, Set, Remove, GetOrCreate)
IEventBus             // messaging (PublishAsync)
ICurrentUser          // identity context (UserId, TenantId, Roles)
IPasswordHasher       // security (Hash, Verify)
IEmailService         // notifications (SendAsync)
```

Each handler only injects the interfaces it actually needs — never a fat "service" class.

### D — Dependency Inversion

High-level modules (Application) depend on abstractions (Core interfaces). Low-level modules (Infrastructure) depend on the same abstractions.

```
Domain        →  no dependencies
Application   →  B2B.Shared.Core interfaces only
Infrastructure → implements Core interfaces (EF Core, Redis, BCrypt, SMTP)
API           →  wires DI; depends on Infrastructure for registration only
```

```csharp
// Application: depends on abstraction
public sealed class LoginHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens) { }

// Infrastructure: provides the concrete implementations
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
services.AddScoped<ITokenService, TokenService>();
```

---

## Design Patterns

### Creational

| Pattern | Where used | Purpose |
|---|---|---|
| **Factory Method** | `Order.Create()`, `Product.Create()`, `Address.Create()`, `Error.NotFound()` | Enforce invariants at construction; hide `new` |
| **Builder** (implicit) | `WebApplication.CreateBuilder(args)` | Fluent service registration |

```csharp
// Factory Method — invalid state is impossible to construct
var address = Address.Create("123 Main St", "New York", "NY", "10001", "US");
var price   = Money.Of(99.99m, "USD");
var order   = OrderEntity.Create(userId, tenantId, address);
```

### Structural

| Pattern | Where used | Purpose |
|---|---|---|
| **Decorator** | MediatR pipeline behaviors | Add cross-cutting concerns without touching handlers |
| **Facade** | `AddSharedInfrastructure()`, `AddEventBus()` | Hide multi-step DI wiring behind a single call |
| **Proxy** | YARP Gateway | Transparent reverse proxy with rate limiting and JWT validation |
| **Composite** | `PagedList<T>` | Wraps `IReadOnlyList<T>` with pagination metadata |

```csharp
// Decorator — each behavior wraps the next without knowing what it is
LoggingBehavior → ValidationBehavior → DomainEventBehavior → Handler
```

### Behavioral

| Pattern | Where used | Purpose |
|---|---|---|
| **Mediator** | MediatR (`ISender`) | Decouples commands/queries from handlers; zero direct handler references in controllers |
| **Chain of Responsibility** | MediatR pipeline | Each behavior decides whether to pass to the next |
| **Observer** | Domain Events + MassTransit consumers | Aggregates raise events; handlers react without coupling |
| **Strategy** | `IPasswordHasher`, `ICacheService`, `IEmailService` | Swap algorithm/provider at DI registration time |
| **Template Method** | `BaseRepository<T>`, `BaseDbContext` | Shared EF Core scaffolding; services override only what differs |
| **Repository** | `IOrderRepository`, `IProductRepository` | Abstract persistence behind a collection-like interface |
| **Unit of Work** | `IUnitOfWork` / `BaseDbContext` | Batch changes into a single atomic commit |
| **Outbox** | MassTransit in-memory outbox | Guarantee at-least-once integration event delivery |
| **Cache-Aside** | `ICacheService.GetOrCreateAsync` | Load from cache; fall back to DB; repopulate on miss |

```csharp
// Mediator — controller has zero knowledge of handler implementations
[HttpPost]
public async Task<IActionResult> Create(CreateOrderCommand cmd)
    => (await sender.Send(cmd)).ToActionResult();

// Observer — aggregate raises event; notification worker reacts independently
order.Confirm();  // raises OrderConfirmedEvent internally
// → DomainEventBehavior publishes it after SaveChangesAsync
// → Notification Worker consumes OrderConfirmedIntegration via Kafka topic

// Strategy — swap password hasher with zero handler changes
services.AddScoped<IPasswordHasher, BcryptPasswordHasher>(); // or ArgonPasswordHasher
```

### Architectural

| Pattern | Where used | Purpose |
|---|---|---|
| **Clean Architecture** | All services | Dependency rule: outer layers depend on inner, never reverse |
| **CQRS** | All services | Separate read model (queries) from write model (commands) |
| **DDD Aggregates** | `Order`, `Product`, `User` | Enforce consistency boundary; single entry point for mutations |
| **Outbox** | MassTransit | Reliable async messaging without distributed transactions |
| **API Gateway** | YARP | Single ingress: routing, rate-limiting, auth, load balancing |
| **Multi-tenancy** | All entities | Row-level isolation via `TenantId` on every query |

---

## Architecture Patterns

**Clean Architecture** — each service has Domain → Application → Infrastructure → API layers with strict dependency direction inward.

**CQRS** — commands return `Result<TResponse>`, queries return paged results. MediatR 12 pipeline: Logging → Validation → DomainEvent publishing.

**Result Pattern** — `Result<T>` with typed `Error` records (NotFound, Validation, Conflict, Unauthorized, Forbidden). No exceptions for business errors.

**Domain-Driven Design** — Aggregate roots with domain events, value objects, entity base classes. Events raised during aggregate mutations, published after `SaveChangesAsync`.

**Multi-tenancy** — TenantId on all entities. `ICurrentUser` exposes `UserId`, `TenantId`, `TenantSlug` from JWT claims.

**Outbox Pattern** — MassTransit in-memory outbox ensures at-least-once delivery of integration events.

## Building Blocks

### Aggregate Root

Base class for all DDD aggregates. Holds domain events and exposes `RaiseDomainEvent` / `ClearDomainEvents`.

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### Entity

Base class for all entities. Equality is identity-based (by `Id`), not reference-based.

```csharp
public abstract class Entity<TId> : IAuditableEntity
{
    public TId Id { get; protected init; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### Value Object

Base class for immutable value types. Equality is component-based.

```csharp
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();
}

// Example
public sealed class Address : ValueObject
{
    public static Address Create(string street, string city, string state, string postalCode, string country) => new(...);
    protected override IEnumerable<object?> GetEqualityComponents() =>
        [Street, City, State, PostalCode, Country];
}
```

### Domain Event

Immutable record raised inside an aggregate, published after `SaveChangesAsync` by `DomainEventBehavior`.

```csharp
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

// Define events as records
public record OrderConfirmedEvent(Guid OrderId, string OrderNumber, Guid CustomerId) : DomainEvent;
```

### Result Pattern

Commands never throw business errors — they return `Result<T>`. The API controller maps `ErrorType` to HTTP status codes.

```csharp
// Success
return new CreateOrderResponse(order.Id, order.OrderNumber, order.TotalAmount);

// Failure — picked up by middleware/controller
return Error.NotFound("Order.NotFound", $"Order {id} not found.");
return Error.Validation("Order.EmptyItems", "Order must contain at least one item.");
return Error.Conflict("Product.SkuExists", $"SKU '{sku}' already exists.");
```

### CQRS Command / Query

```csharp
// Command — mutates state, returns Result<TResponse>
public sealed record CreateOrderCommand(AddressDto ShippingAddress, List<OrderItemDto> Items, string? Notes)
    : ICommand<CreateOrderResponse>;

// Query — reads state, no side effects
public sealed record GetOrdersQuery(int Page, int PageSize)
    : IQuery<PagedList<OrderSummaryDto>>;
```

### MediatR Pipeline (order of execution)

```
Request → LoggingBehavior → ValidationBehavior → Handler → DomainEventBehavior → Response
```

| Behavior | Responsibility |
|---|---|
| `LoggingBehavior` | Logs request name and elapsed ms |
| `ValidationBehavior` | Runs FluentValidation; short-circuits on first error |
| `DomainEventBehavior` | Publishes domain events from EF ChangeTracker after handler completes |

### Repository Pattern

```csharp
// Generic base — covers most CRUD
public interface IRepository<TEntity, TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}

// Service-specific extension
public interface IOrderRepository : IRepository<OrderEntity, Guid>
{
    Task<PagedList<OrderEntity>> GetByCustomerAsync(Guid customerId, Guid tenantId, int page, int pageSize, CancellationToken ct);
}
```

### ICacheService

```csharp
// Cache-aside pattern
var products = await cache.GetOrCreateAsync(
    key: $"products:tenant:{tenantId}:page:{page}",
    factory: async () => await productRepository.GetPagedAsync(...),
    expiry: TimeSpan.FromMinutes(5));

// Invalidate on mutation
await cache.RemoveByPrefixAsync($"products:tenant:{tenantId}");
```

### Integration Events (cross-service messaging)

Integration events are separate from domain events — they cross service boundaries via Apache Kafka.

```csharp
// Publisher (inside a domain event handler or consumer)
await eventBus.PublishAsync(new OrderConfirmedIntegration(order.Id, order.OrderNumber, ...));

// Consumer (Notification Worker)
public class OrderConfirmedConsumer : IConsumer<OrderConfirmedIntegration>
{
    public async Task Consume(ConsumeContext<OrderConfirmedIntegration> context) { ... }
}
```

### Multi-tenancy

Every entity stores `TenantId`. `ICurrentUser` is resolved from the JWT and injected into all handlers.

```csharp
// Always scope queries to the current tenant
var orders = await context.Orders
    .Where(o => o.TenantId == currentUser.TenantId)
    .ToListAsync(ct);
```

## Key Shared Abstractions (`B2B.Shared.Core`)

```csharp
// All business errors
Error.NotFound("Category.NotFound", "Category not found.")
Error.Validation("Order.EmptyItems", "Order must contain at least one item.")
Error.Conflict("Product.SkuExists", $"SKU '{sku}' already exists.")

// Paged queries
PagedList<T>.CreateAsync(query, page, pageSize)

// Domain events - raised in aggregates, published by DomainEventBehavior
record ProductCreatedEvent(Guid ProductId) : DomainEvent;
```

## Type Alias Convention

`Product` and `Order` are both namespace names and entity names. **Always use type aliases** in files under those namespaces:

```csharp
using ProductEntity = B2B.Product.Domain.Entities.Product;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderItemEntity = B2B.Order.Domain.Entities.OrderItem;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;
```

This applies to: repositories, handlers, DbContext, test files.

## MediatR 12 Pipeline Behavior

`RequestHandlerDelegate<TResponse>` takes **no parameters** — call `await next()`, not `await next(ct)`:

```csharp
// Correct
var response = await next();
// Wrong - compile error
var response = await next(cancellationToken);
```

## Password Hashing

`IPasswordHasher` (in `B2B.Shared.Core`) abstracts BCrypt. Application handlers depend only on the interface; `BcryptPasswordHasher` in `B2B.Shared.Infrastructure` implements it. Never reference `BCrypt.Net` directly in Application or Domain layers.

## IAuditableEntity Location

`IAuditableEntity` lives in `B2B.Shared.Core/Interfaces/` (not Infrastructure) to avoid a Clean Architecture violation where Domain entities would depend on Infrastructure.

## Health Checks Namespace

The correct namespace for `UIResponseWriter` is `HealthChecks.UI.Client`, **not** `AspNetCore.HealthChecks.UI.Client`:

```csharp
using HealthChecks.UI.Client; // correct
```

## Infrastructure Stack

| Service    | Technology                        | Port  |
|------------|-----------------------------------|-------|
| Gateway    | YARP + Rate Limiter               | 5000  |
| Identity   | ASP.NET Core 9 + EF Core 9        | 5001  |
| Product    | ASP.NET Core 9 + EF Core 9        | 5002  |
| Order      | ASP.NET Core 9 + EF Core 9        | 5003  |
| Database   | PostgreSQL 16                     | 5432  |
| Cache      | Redis 7                           | 6379  |
| Messaging  | Apache Kafka 3.7 KRaft (MassTransit) | 9092  |
| Kafka UI   | provectuslabs/kafka-ui            | 8090  |
| Tracing    | Jaeger                            | 16686 |
| Logging    | Seq                               | 5341  |
| Email      | MailHog (dev)                     | 8025  |
| DB Admin   | pgAdmin 4                         | 5050  |

## Package Versions (Directory.Packages.props)

EF Core packages must be **9.0.3** (not 9.0.0) to satisfy Npgsql 9.0.3's minimum version constraint.

## Running Locally

```bash
# Start all infrastructure
docker-compose up -d

# Run specific service
dotnet run --project src/Services/Identity/B2B.Identity.Api

# Run tests
dotnet test B2B.sln
```

## Adding a New Microservice

1. Create four projects: `Domain`, `Application`, `Infrastructure`, `Api`
2. Domain references only `B2B.Shared.Core`
3. Application references Domain and `B2B.Shared.Core`
4. Infrastructure references Application and `B2B.Shared.Infrastructure`
5. Api references Infrastructure; registers via `AddSharedInfrastructure` + `AddEventBus`
6. Add type alias if service name matches namespace (see Type Alias Convention above)
7. Add YARP route in `B2B.Gateway/appsettings.json`
8. Add service to `docker-compose.yml` and `docker-compose.override.yml`
9. Add database creation to `infrastructure/postgres/init.sql`
