# B2B Microservice — Modern Architecture

Production-grade B2B microservice template built on **ASP.NET Core 9**, **Clean Architecture**, **CQRS**, **DDD**, and **event-driven messaging** (MassTransit + RabbitMQ).

This README is the **technical entry point for contributors**. For day-to-day workflow rules see [CONTRIBUTING.md](CONTRIBUTING.md). For deeper design see [docs/HLD.md](docs/HLD.md) and [docs/LLD.md](docs/LLD.md).

---

## 1. At a Glance

| | |
|---|---|
| **Runtime** | .NET 9 (rolls forward to 10) |
| **Style** | Clean Architecture per service · CQRS via MediatR 12 · Result pattern |
| **Persistence** | EF Core 9 · PostgreSQL 16 (one DB per service) · PgBouncer (transaction-mode pooling) |
| **Cache** | HybridCache (L1 in-process 2 min + L2 Redis 15 min) · Redis 7 (cache-aside via `ICacheService`) |
| **Messaging** | MassTransit + Apache Kafka 3.7 KRaft (Kafka Rider, in-memory outbox) |
| **Edge** | YARP gateway (per-tenant rate-limit + JWT validation) |
| **Resilience** | Polly 8 three-layer pipeline: Bulkhead → Circuit Breaker → Retry (exponential + jitter) |
| **Observability** | Jaeger (traces) · Seq (logs) · Prometheus + Grafana (metrics) · OTel Collector · Split `/health/live` + `/health/ready` |
| **Tests** | xUnit + FluentAssertions + NSubstitute — **586 tests, 0 failures** |

---

## 2. Repository Layout

```
src/
  Gateway/B2B.Gateway/                 # YARP reverse proxy                :5000
  Shared/
    B2B.Shared.Core/                   # Domain abstractions, CQRS, Result, Errors
    B2B.Shared.Infrastructure/         # EF base, MassTransit, Redis, MediatR behaviors
  Services/
    Identity/  { Domain | Application | Infrastructure | Api }             :5001
    Product/   { Domain | Application | Infrastructure | Api }             :5002
    Order/     { Domain | Application | Infrastructure | Api }             :5003
    Basket/    { Domain | Application | Infrastructure | Api }             :5004
    Payment/   { Domain | Application | Infrastructure | Api }             :5005
    Shipping/  { Domain | Application | Infrastructure | Api }             :5006
    Vendor/    { Domain | Application | Infrastructure | Api }             :5007
    Discount/  { Domain | Application | Infrastructure | Api }             :5008
    Review/    { Domain | Application | Infrastructure | Api }             :5011
  Workers/
    B2B.Notification.Worker/           # MassTransit consumers, SMTP email
tests/
  B2B.Identity.Tests/                  # 135 tests
  B2B.Product.Tests/                   # 55 tests
  B2B.Order.Tests/                     # 69 tests
  B2B.Shared.Tests/                    # 12 tests
  B2B.Basket.Tests/                    # 52 tests
  B2B.Payment.Tests/                   # 70 tests
  B2B.Shipping.Tests/                  # 23 tests
  B2B.Discount.Tests/                  # 76 tests
  B2B.Review.Tests/                    # 35 tests
  B2B.Vendor.Tests/                    # 59 tests
infrastructure/
  postgres/init.sql                    # Creates per-service databases
docs/
  BRD.md  HLD.md  LLD.md
```

Each service is **four projects** following the dependency rule:

```
Domain         → (no dependencies)
Application    → Domain + Shared.Core
Infrastructure → Application + Shared.Infrastructure
Api            → Infrastructure
```

Reverse references are a build break, not a code-review nit.

---

## 3. Architectural Pillars

### Clean Architecture
Strict inward dependencies. Domain has zero framework references. Application depends only on `B2B.Shared.Core` abstractions.

### CQRS (MediatR 12)
- **Commands** mutate state, return `Result<TResponse>`.
- **Queries** read state, return DTOs / `PagedList<T>`.
- Pipeline order: `LoggingBehavior → RetryBehavior → IdempotencyBehavior → PerformanceBehavior → AuthorizationBehavior → ValidationBehavior → AuditBehavior → DomainEventBehavior → Handler`.
- Cross-cutting concerns plug in as new behaviors — handlers stay untouched.

### Result Pattern
Business failures are values, not exceptions:
```csharp
return Error.NotFound("Order.NotFound", $"Order {id} not found.");
return Error.Validation("Basket.Empty", "Basket has no items.");
return Error.Conflict("Vendor.TaxIdExists", $"Tax ID already registered.");
```
Exceptions are reserved for invariants and bugs. Controllers map `ErrorType` → HTTP status.

### Domain-Driven Design
- `AggregateRoot<TId>` raises domain events; `DomainEventBehavior` publishes them after `SaveChangesAsync`.
- Value objects (`Address`, `Money`) constructed via static factories that enforce invariants.
- Child entities mutate **only** through their aggregate root.

### Event-Driven Messaging
- **Domain events** stay inside a service.
- **Integration events** (`*Integration` records) cross service boundaries via Apache Kafka with the MassTransit in-memory outbox for at-least-once delivery.
- Notification Worker is a pure consumer — no HTTP surface.

### Multi-tenancy
Every entity carries `TenantId`. Entities that implement `ITenantEntity` get an **automatic global EF Core query filter** applied at `BaseDbContext` level — no manual `.Where(e => e.TenantId == ...)` is required in handlers or repositories. Background services call `IgnoreQueryFilters()` explicitly when they need to scan across tenants.

---

## 4. Quick Start

```bash
git clone <repo>
cd "B2B Microservice Modern Architecture"

# 1. Bring up infra (Postgres, Redis, Kafka, Jaeger, Seq, MailHog, pgAdmin)
docker compose up -d

# 2. Build
dotnet build B2B.sln

# 3. Run services (separate terminals)
dotnet run --project src/Services/Identity/B2B.Identity.Api
dotnet run --project src/Services/Product/B2B.Product.Api
dotnet run --project src/Services/Order/B2B.Order.Api
dotnet run --project src/Gateway/B2B.Gateway

# 4. Run all 586 tests
dotnet test B2B.sln
```

### Local URLs

| URL | Purpose |
|---|---|
| http://localhost:5000  | Gateway (entry point) |
| http://localhost:5001  | Identity Service (direct debug) |
| http://localhost:5002  | Product Service (direct debug) |
| http://localhost:5003  | Order Service (direct debug) |
| http://localhost:5004  | Basket Service (direct debug) |
| http://localhost:5005  | Payment Service (direct debug) |
| http://localhost:5006  | Shipping Service (direct debug) |
| http://localhost:5007  | Vendor Service (direct debug) |
| http://localhost:5008  | Discount Service (direct debug) |
| http://localhost:5011  | Review Service (direct debug) |
| http://localhost:16686 | Jaeger UI |
| http://localhost:5341  | Seq logs |
| http://localhost:8025  | MailHog (outgoing mail) |
| http://localhost:5050  | pgAdmin |
| http://localhost:8090  | Kafka UI |
| http://localhost:6432  | PgBouncer (connection pooler, internal) |
| http://localhost:9090  | Prometheus |
| http://localhost:3000  | Grafana dashboards |

---

## 5. Conventions Contributors Must Know

### 5.1 Type aliases for namespace-colliding services
`Order`, `Product`, and `Vendor` are both namespace names and entity names. Inside files in those namespaces, **always alias**:
```csharp
using OrderEntity    = B2B.Order.Domain.Entities.Order;
using OrderItemEntity = B2B.Order.Domain.Entities.OrderItem;
using OrderStatus    = B2B.Order.Domain.Entities.OrderStatus;
using ProductEntity  = B2B.Product.Domain.Entities.Product;
using VendorEntity   = B2B.Vendor.Domain.Entities.Vendor;
```
Applies to repositories, handlers, DbContext, and tests.

### 5.2 MediatR 12 pipeline signature
`RequestHandlerDelegate<TResponse>` takes **no parameters**:
```csharp
var response = await next();          // correct
var response = await next(ct);        // compile error
```

### 5.3 Password hashing
Application depends on `IPasswordHasher` only. Never reference `BCrypt.Net` outside `B2B.Shared.Infrastructure`.

### 5.4 `IAuditableEntity` location
Lives in `B2B.Shared.Core/Interfaces/` — not Infrastructure. Domain entities depending on Infrastructure is a Clean Architecture violation.

### 5.5 Health-check namespace
```csharp
using HealthChecks.UI.Client;            // correct
// using AspNetCore.HealthChecks.UI.Client;  // wrong
```

Health endpoints are split:
- `/health/live` — liveness probe (no dependency checks; returns 200 immediately)
- `/health/ready` — readiness probe (checks PostgreSQL + Redis tagged `"ready"`; Kafka validated at startup by MassTransit Rider host)
- `/health` — legacy all-checks endpoint

### 5.6 Package versions
EF Core packages must be **9.0.3** (Npgsql 9.0.3 minimum constraint). Pinned in [Directory.Packages.props](Directory.Packages.props).

---

## 6. Service Summary

| Service | Port | DB | Key Entities | Highlights |
|---|---|---|---|---|
| Identity | 5001 | `b2b_identity` | User, Tenant, RefreshToken | BCrypt, JWT, refresh rotation |
| Product | 5002 | `b2b_product` | Product, Category | Low-stock events, cache-aside |
| Order | 5003 | `b2b_order` | Order, OrderItem | Idempotency, Saga, state machine |
| Basket | 5004 | Redis only | Basket, BasketItem | Ephemeral, Redis-native, checkout |
| Payment | 5005 | `b2b_payment` | Payment, Invoice | Invoice lifecycle, refund, receipt |
| Shipping | 5006 | `b2b_shipping` | Shipment | Carrier, tracking, dispatch, delivery |
| Vendor | 5007 | `b2b_vendor` | Vendor | Approval, suspend, commission rate |
| Discount | 5008 | `b2b_discount` | Discount, Coupon | Rules, coupon codes, validation |
| Review | 5011 | `b2b_review` | Review | Submit, moderate, approve/reject |
| Notification Worker | — | — | — | 9 consumer types, SMTP email |

---

## 7. Adding a New Microservice

1. Create four projects: `Domain`, `Application`, `Infrastructure`, `Api`.
2. Wire references following the dependency rule (§2).
3. If the service name collides with an entity name, add type aliases (§5.1).
4. Register infra in `Program.cs` via `AddSharedInfrastructure()` + `AddEventBus()`.
5. Add a YARP route in [src/Gateway/B2B.Gateway/appsettings.json](src/Gateway/B2B.Gateway/appsettings.json).
6. Add the service to [docker-compose.yml](docker-compose.yml) and [docker-compose.override.yml](docker-compose.override.yml).
7. Create the database in [infrastructure/postgres/init.sql](infrastructure/postgres/init.sql).
8. Add a `tests/B2B.<Name>.Tests/` project (xUnit + FluentAssertions) and register it in `B2B.sln`.

---

## 8. Where to Read More

| Document | Purpose |
|---|---|
| [CONTRIBUTING.md](CONTRIBUTING.md) | Branching, commits, code review, PR checklist |
| [CLAUDE.md](CLAUDE.md) | Authoritative SOLID / patterns / building-blocks reference |
| [docs/BRD.md](docs/BRD.md) | Business requirements |
| [docs/HLD.md](docs/HLD.md) | High-level design |
| [docs/LLD.md](docs/LLD.md) | Low-level design |

---

## 9. Troubleshooting

| Symptom | Likely cause |
|---|---|
| `CS0118: 'Vendor' is a namespace but is used like a type` | Missing type alias; add `using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;` |
| `CS1022: Type or namespace definition, or end-of-file expected` | Stray `}` at end of file |
| `Could not resolve Npgsql 9.0.3` | EF Core packages not pinned to 9.0.3 |
| `401` from gateway | JWT issuer/audience mismatch with `Identity.Api` config |
| `503 Service Unavailable` from API | Bulkhead saturated (too many concurrent calls) or circuit breaker open (too many recent failures) |
| Domain events never fire | Handler returned before `SaveChangesAsync`, or aggregate not tracked by `ChangeTracker` |
| Consumer not receiving messages | Kafka container down, or contract record namespace differs between publisher and consumer, or consumer group offset misconfigured |
| Basket returns 404 after restart | Expected — basket is Redis-only (ephemeral); no persistent backing store |
| Global tenant filter returning wrong rows | Entity does not implement `ITenantEntity`; apply `IgnoreQueryFilters()` explicitly for cross-tenant background queries |
