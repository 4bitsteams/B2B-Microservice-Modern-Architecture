# B2B Microservice — Modern Architecture

Production-grade B2B microservice template built on **ASP.NET Core 9**, **Clean Architecture**, **CQRS**, **DDD**, and **event-driven messaging** (MassTransit + RabbitMQ).

This README is the **technical entry point for contributors**. For day-to-day workflow rules see [CONTRIBUTING.md](CONTRIBUTING.md). For deeper design see [docs/HLD.md](docs/HLD.md) and [docs/LLD.md](docs/LLD.md).

---

## 1. At a Glance

| | |
|---|---|
| **Runtime** | .NET 9 (rolls forward to 10) |
| **Style** | Clean Architecture per service · CQRS via MediatR 12 · Result pattern |
| **Persistence** | EF Core 9 · PostgreSQL 16 (one DB per service) |
| **Cache** | Redis 7 (cache-aside) |
| **Messaging** | MassTransit + RabbitMQ 3 (in-memory outbox) |
| **Edge** | YARP gateway (rate-limit + JWT validation) |
| **Observability** | Jaeger (traces) · Seq (logs) · ASP.NET HealthChecks |
| **Tests** | xUnit + FluentAssertions |

---

## 2. Repository Layout

```
src/
  Gateway/B2B.Gateway/                 # YARP reverse proxy        :5000
  Shared/
    B2B.Shared.Core/                   # Domain abstractions, CQRS, Result, Errors
    B2B.Shared.Infrastructure/         # EF base, MassTransit, Redis, MediatR behaviors
  Services/
    Identity/  { Domain | Application | Infrastructure | Api }   :5001
    Product/   { Domain | Application | Infrastructure | Api }   :5002
    Order/     { Domain | Application | Infrastructure | Api }   :5003
  Workers/
    B2B.Notification.Worker/           # MassTransit consumers, SMTP email
tests/
  B2B.Identity.Tests/
  B2B.Product.Tests/                   # xUnit
  B2B.Order.Tests/
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
- Pipeline order: `LoggingBehavior → ValidationBehavior → Handler → DomainEventBehavior`.
- Cross-cutting concerns (idempotency, retry, caching) plug in as new behaviors — handlers stay untouched.

### Result Pattern
Business failures are values, not exceptions:
```csharp
return Error.NotFound("Order.NotFound", $"Order {id} not found.");
```
Exceptions are reserved for invariants and bugs. Controllers map `ErrorType` → HTTP status.

### Domain-Driven Design
- `AggregateRoot<TId>` raises domain events; `DomainEventBehavior` publishes them after `SaveChangesAsync`.
- Value objects (`Address`, `Money`) constructed via static factories that enforce invariants.
- Child entities mutate **only** through their aggregate root.

### Event-Driven Messaging
- **Domain events** stay inside a service.
- **Integration events** (`*Integration` records) cross service boundaries via RabbitMQ with the MassTransit in-memory outbox for at-least-once delivery.
- Notification Worker is a pure consumer — no HTTP surface.

### Multi-tenancy
Every entity carries `TenantId`. `ICurrentUser` (resolved from JWT) is injected into handlers; queries must always filter by `currentUser.TenantId`.

---

## 4. Quick Start

```bash
git clone <repo>
cd B2B-Microservice-Modern-Architecture

# 1. Bring up infra (Postgres, Redis, RabbitMQ, Jaeger, Seq, MailHog, pgAdmin)
docker compose up -d

# 2. Build
dotnet build B2B.sln

# 3. Run a service
dotnet run --project src/Services/Identity/B2B.Identity.Api
dotnet run --project src/Services/Product/B2B.Product.Api
dotnet run --project src/Services/Order/B2B.Order.Api
dotnet run --project src/Gateway/B2B.Gateway

# 4. Tests
dotnet test B2B.sln
```

### Local URLs

| URL | Purpose |
|---|---|
| http://localhost:5000  | Gateway (entry point) |
| http://localhost:5001-5003 | Direct service ports (debug only) |
| http://localhost:16686 | Jaeger UI |
| http://localhost:5341  | Seq logs |
| http://localhost:8025  | MailHog (outgoing mail) |
| http://localhost:5050  | pgAdmin |
| http://localhost:15672 | RabbitMQ management |

---

## 5. Conventions Contributors Must Know

### 5.1 Type aliases for `Order` / `Product`
Both names are also namespace names. Inside files in those namespaces, **always alias**:
```csharp
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderItemEntity = B2B.Order.Domain.Entities.OrderItem;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;
using ProductEntity = B2B.Product.Domain.Entities.Product;
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

### 5.6 Package versions
EF Core packages must be **9.0.3** (Npgsql 9.0.3 minimum constraint). Pinned in [Directory.Packages.props](Directory.Packages.props).

---

## 6. Adding a New Microservice

1. Create four projects: `Domain`, `Application`, `Infrastructure`, `Api`.
2. Wire references following the dependency rule (§2).
3. If the service name collides with an entity name, add type aliases (§5.1).
4. Register infra in `Program.cs` via `AddSharedInfrastructure()` + `AddEventBus()`.
5. Add a YARP route in [src/Gateway/B2B.Gateway/appsettings.json](src/Gateway/B2B.Gateway/appsettings.json).
6. Add the service to [docker-compose.yml](docker-compose.yml) and [docker-compose.override.yml](docker-compose.override.yml).
7. Create the database in [infrastructure/postgres/init.sql](infrastructure/postgres/init.sql).
8. Add a `tests/B2B.<Name>.Tests/` project (xUnit + FluentAssertions).

---

## 7. Where to Read More

| Document | Purpose |
|---|---|
| [CONTRIBUTING.md](CONTRIBUTING.md) | Branching, commits, code review, PR checklist |
| [CLAUDE.md](CLAUDE.md) | Authoritative SOLID / patterns / building-blocks reference |
| [docs/BRD.md](docs/BRD.md) | Business requirements |
| [docs/HLD.md](docs/HLD.md) | High-level design |
| [docs/LLD.md](docs/LLD.md) | Low-level design |

---

## 8. Troubleshooting

| Symptom | Likely cause |
|---|---|
| `CS1022: Type or namespace definition, or end-of-file expected` | Stray `}` at end of file |
| `Could not resolve Npgsql 9.0.3` | EF Core packages not pinned to 9.0.3 |
| `401` from gateway | JWT issuer/audience mismatch with `Identity.Api` config |
| Domain events never fire | Handler returned before `SaveChangesAsync`, or aggregate not tracked by `ChangeTracker` |
| Consumer not receiving messages | RabbitMQ container down, or contract record namespace differs between publisher and consumer |
