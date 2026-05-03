# Contributing to B2B Microservice Platform

Welcome. This document is the **operating manual** for working in this repo: how to set up, what conventions to follow, how to add a feature, how to get a PR merged.

If you're new, also read [docs/HLD.md](docs/HLD.md) and [docs/LLD.md](docs/LLD.md) first.

---

## 1. Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 9.0 or 10.0 (see `global.json` — `rollForward: latestMajor` accepts either) |
| Docker Desktop | latest |
| Git | 2.40+ |
| IDE | Visual Studio 2022 17.12+, Rider 2024.3+, or VS Code with C# Dev Kit |

Verify:

```bash
dotnet --version       # 9.x or 10.x
docker --version
docker compose version
```

## 2. Local Setup

```bash
git clone <repo>
cd "B2B Microservice Modern Architecture"

# 1. Bring up the infra stack
docker compose up -d

# 2. Restore + build
dotnet build B2B.sln

# 3. Run a service (in separate terminals)
dotnet run --project src/Services/Identity/B2B.Identity.Api
dotnet run --project src/Services/Product/B2B.Product.Api
dotnet run --project src/Services/Order/B2B.Order.Api
dotnet run --project src/Gateway/B2B.Gateway

# 4. Run all 586 tests
dotnet test B2B.sln
```

After step 1, the following are available locally:

| URL | Purpose |
|---|---|
| http://localhost:5000 | Gateway (your entry point) |
| http://localhost:5001 | Identity Service (direct debug) |
| http://localhost:5002 | Product Service (direct debug) |
| http://localhost:5003 | Order Service (direct debug) |
| http://localhost:5004 | Basket Service (direct debug) |
| http://localhost:5005 | Payment Service (direct debug) |
| http://localhost:5006 | Shipping Service (direct debug) |
| http://localhost:5007 | Vendor Service (direct debug) |
| http://localhost:5008 | Discount Service (direct debug) |
| http://localhost:5011 | Review Service (direct debug) |
| http://localhost:16686 | Jaeger UI |
| http://localhost:5341 | Seq logs |
| http://localhost:8025 | MailHog (catches outgoing email) |
| http://localhost:5050 | pgAdmin |
| http://localhost:8090  | Kafka UI (browse topics, consumer groups, offsets) |
| http://localhost:9090  | Prometheus |
| http://localhost:3000  | Grafana dashboards |
| localhost:6432         | PgBouncer (Postgres connection pooler — internal use) |

## 3. Branching & Commits

- `main` is always green and deployable. No direct pushes.
- Branch names: `feat/<topic>`, `fix/<topic>`, `chore/<topic>`, `docs/<topic>`.
- Conventional commits:

  ```
  feat(order): add idempotency to CreateOrder
  fix(identity): refresh token rotation off-by-one
  docs(hld): clarify outbox roadmap
  chore(deps): bump MassTransit to 8.3.6
  ```

- Keep commits small and focused. Squash-merge is the default.

## 4. Code Standards

### 4.1 Architecture rules (enforced at review)

- **Dependency direction.** Domain → nothing. Application → Core. Infrastructure → Application + Core. API → Infrastructure. Never reverse.
- **No business exceptions.** Return `Result.Failure(Error.X(...))`. Throw only for invariants and unexpected bugs.
- **Aggregate-only mutation.** Child entities mutated through their root only. No child-only repository.
- **Multi-tenancy.** Entities implementing `ITenantEntity` receive an automatic global EF query filter — no manual `.Where(e => e.TenantId == ...)` required in handlers or repositories. Background services that need to scan all tenants must call `IgnoreQueryFilters()` explicitly. PRs that add a new entity without `ITenantEntity` (when multi-tenancy applies) will be blocked.
- **CQRS purity.** Commands return `Result` / `Result<T>`. Queries return `Result<T>` and have no side-effects.
- **Type aliases.** Use aliases in all files under `B2B.Order.*`, `B2B.Product.*`, and `B2B.Vendor.*`:
  ```csharp
  using OrderEntity    = B2B.Order.Domain.Entities.Order;
  using ProductEntity  = B2B.Product.Domain.Entities.Product;
  using VendorEntity   = B2B.Vendor.Domain.Entities.Vendor;
  using VendorStatus   = B2B.Vendor.Domain.Entities.VendorStatus;
  ```
- **MediatR 12.** `await next();` — no parameters. `await next(ct);` is a compile error.

### 4.2 SOLID checklist

- **S** — One class, one reason to change. If a handler imports five repositories, split it.
- **O** — Add new behavior via new pipeline behaviors / new handlers. Don't modify existing ones to add cross-cutting concerns.
- **L** — All `IRepository<>` / `ICacheService` / `IEmailService` implementations must be substitutable in tests without changing handler code.
- **I** — Don't add methods to `IRepository<>` for one consumer's needs. Extend it on the service-specific interface.
- **D** — Application code depends on `B2B.Shared.Core` interfaces. Never reference `BCrypt.Net`, `StackExchange.Redis`, `Npgsql`, or `MassTransit` directly from Application or Domain.

### 4.3 Style

- C# 13 features welcome (primary constructors, collection expressions, file-scoped namespaces).
- `sealed` by default on classes that don't need to be inherited.
- `record` for immutable DTOs and value objects.
- Async ends in `Async` and takes `CancellationToken ct = default` last.
- One public type per file.
- No unused `using` directives.

### 4.4 Comments

- Comment **why**, not **what**.
- Don't reference tickets or PRs in code comments — those belong in commit messages.
- Public types on shared abstractions (`B2B.Shared.Core`) require XML doc comments. Service-specific internal types do not.

## 5. Adding a Feature — Walkthrough

### 5.1 Add a Command (e.g. CancelOrder)

1. **Domain** — add `Order.Cancel(reason)` if not present, raising `OrderCancelledEvent`.
2. **Application/Commands/CancelOrder/**
   - `CancelOrderCommand.cs` — `ICommand` (or `ICommand<TResponse>`).
   - `CancelOrderHandler.cs` — `ICommandHandler<CancelOrderCommand>`.
   - `CancelOrderValidator.cs` — `AbstractValidator<CancelOrderCommand>` (auto-registered).
3. **API/Controllers/OrdersController.cs** — add `Cancel(Guid id, ...)`.
4. **Authorizer (if resource-based)** — `{Name}Authorizer.cs : IAuthorizer<{Name}Command>`:
   ```csharp
   services.AddScoped<IAuthorizer<CancelOrderCommand>, CancelOrderAuthorizer>();
   ```
5. **Test** — `{Name}HandlerTests.cs` (all success/error branches) + `{Name}AuthorizerTests.cs`.

### 5.2 Add a Query

Use `IQuery<TResponse>` / `IQueryHandler<,>`. Queries should hit the cache where reasonable:

```csharp
return await cache.GetOrCreateAsync(
    key:     $"orders:tenant:{currentUser.TenantId}:page:{request.Page}",
    factory: () => repository.GetPagedAsync(...),
    expiry:  TimeSpan.FromMinutes(2));
```

### 5.3 Add a Domain Event Handler

```csharp
public sealed class WhenOrderConfirmed_PublishIntegration(IEventBus bus)
    : INotificationHandler<OrderConfirmedEvent>
{
    public async Task Handle(OrderConfirmedEvent e, CancellationToken ct) =>
        await bus.PublishAsync(new OrderConfirmedIntegration(...), ct);
}
```

It is auto-discovered by MediatR's assembly scan.

### 5.4 Add a Worker Consumer

In `B2B.Notification.Worker/Consumers/`:

```csharp
public sealed class MyEventConsumer : IConsumer<MyIntegration>
{
    public Task Consume(ConsumeContext<MyIntegration> context) { … }
}
```

Then in `Program.cs`:
```csharp
busConfig.AddConsumer<MyEventConsumer>();
```

Integration event contracts belong in `B2B.Shared.Core/Messaging/IntegrationEvents.cs`.

## 6. Adding a New Microservice

See [LLD §12](docs/LLD.md). Summarised checklist:

- [ ] Four projects (`Domain`, `Application`, `Infrastructure`, `Api`)
- [ ] Reference graph correct (Domain has zero deps)
- [ ] Standard subfolders present
- [ ] Type alias if name collides (`Vendor`, `Order`, `Product`)
- [ ] `AddSharedInfrastructure` + `AddEventBus` wired
- [ ] YARP route + cluster + health check added to `B2B.Gateway/appsettings.json`
- [ ] `docker-compose.yml` + `docker-compose.override.yml` updated
- [ ] `CREATE DATABASE` line in `infrastructure/postgres/init.sql`
- [ ] Test project under `tests/` and added to `B2B.sln`

## 7. Testing

```bash
# Run all 586 tests
dotnet test B2B.sln

# Single project
dotnet test tests/B2B.Order.Tests/B2B.Order.Tests.csproj
dotnet test tests/B2B.Product.Tests/B2B.Product.Tests.csproj
dotnet test tests/B2B.Identity.Tests/B2B.Identity.Tests.csproj
dotnet test tests/B2B.Shared.Tests/B2B.Shared.Tests.csproj
dotnet test tests/B2B.Basket.Tests/B2B.Basket.Tests.csproj
dotnet test tests/B2B.Payment.Tests/B2B.Payment.Tests.csproj
dotnet test tests/B2B.Shipping.Tests/B2B.Shipping.Tests.csproj
dotnet test tests/B2B.Discount.Tests/B2B.Discount.Tests.csproj
dotnet test tests/B2B.Review.Tests/B2B.Review.Tests.csproj
dotnet test tests/B2B.Vendor.Tests/B2B.Vendor.Tests.csproj

# Single test
dotnet test --filter "FullyQualifiedName~CreateOrderHandlerTests"
```

### Current test suite (all in `B2B.sln`)

| Project | Tests | Focus |
|---|---|---|
| `B2B.Identity.Tests` | 135 | Domain, application handlers (10), validators, token service |
| `B2B.Product.Tests` | 55 | Domain aggregates, value objects, application handlers, cache behavior |
| `B2B.Order.Tests` | 69 | Domain aggregates, value objects, application handlers, authorizers |
| `B2B.Shared.Tests` | 12 | Pipeline behaviors (Validation, Authorization, Idempotency) |
| `B2B.Basket.Tests` | 52 | Domain (basket, item), application handlers (6), validators |
| `B2B.Payment.Tests` | 70 | Domain (payment, invoice), application handlers (9), validators |
| `B2B.Shipping.Tests` | 23 | Domain (shipment states), application handlers (3) |
| `B2B.Discount.Tests` | 76 | Domain (discount, coupon), application handlers (7), validators |
| `B2B.Review.Tests` | 35 | Domain (review states), application handlers (6), validators |
| `B2B.Vendor.Tests` | 59 | Domain (vendor lifecycle), application handlers (7), validators |
| **Total** | **586** | **0 failures** |

### Testing targets

- Domain: ≥ 90 % line coverage on aggregates and value objects.
- Application: every handler has at least one success test and one test per `Error.*` branch it can return.
- Authorizers: every `IAuthorizer<T>` implementation has tests for all success paths and each failure path.
- Validators: every `AbstractValidator<T>` implementation has tests for all rules and boundary values.
- Infrastructure: integration tests with Testcontainers if behavior depends on the real DB or broker.

## 8. Observability During Development

- **Watching logs in real time:** open Seq at http://localhost:5341.
- **Tracing a request end-to-end:** open Jaeger at http://localhost:16686.
- **Viewing metrics and dashboards:** open Grafana at http://localhost:3000 (default admin/admin). Raw Prometheus at http://localhost:9090.
- **Inspecting RabbitMQ queues:** http://localhost:15672 → Queues tab.
- **Reading email:** http://localhost:8025 (MailHog catches everything).
- **Health probes:** `/health/live` (liveness — no checks), `/health/ready` (readiness — DB + Redis + RabbitMQ).

## 9. Database Migrations

```bash
# From the service's Infrastructure project directory:
dotnet ef migrations add MyMigration --startup-project ../B2B.Order.Api
dotnet ef database update --startup-project ../B2B.Order.Api
```

Migrations live under `Persistence/Migrations/`. Squash only with explicit team approval.

**Basket has no migrations** — it is Redis-only.

## 10. Pull Request Process

1. Open a PR against `main`. Title follows conventional-commit format.
2. PR description must include:
   - **What** changed
   - **Why** (link to issue / ticket)
   - **How to verify** (commands, screenshots if API behaviour changed)
3. CI must be green.
4. At least one approving review.
5. Squash-merge.

### Checklist (paste into the PR body)

```markdown
- [ ] Tests added/updated and passing locally (`dotnet test B2B.sln`)
- [ ] No new business exception thrown — used Result + Error
- [ ] New entity implements `ITenantEntity` if it belongs to a specific tenant (global EF filter applied automatically)
- [ ] Background services that scan all tenants use `IgnoreQueryFilters()` explicitly
- [ ] No direct reference to BCrypt.Net / Npgsql / Redis / MassTransit from Application or Domain
- [ ] Type alias used in files under namespaces with name collision (Order, Product, Vendor)
- [ ] Authorization logic extracted to `IAuthorizer<TCommand>` (not embedded in handler)
- [ ] One public type per file
- [ ] Public API change reflected in the relevant doc (HLD/LLD/BRD)
- [ ] New test project added to B2B.sln if applicable
- [ ] New service added to docker-compose.yml and docker-compose.override.yml if applicable
- [ ] New database added to infrastructure/postgres/init.sql if applicable
- [ ] HTTP 503 handled gracefully in client code (bulkhead/circuit breaker can return 503 under high load)
```

## 11. Security & Secrets

- **Never** commit secrets. Local defaults in `docker-compose.override.yml` are for local dev only.
- Rotate the JWT signing key per environment. The repo default is for local dev only.
- Report security findings privately to the security team. Do not open a public issue.

## 12. Getting Help

- Architectural questions → tag `@platform` in Slack `#b2b-platform`.
- Domain questions → the service's CODEOWNERS.
- Build / infra questions → `#dev-tooling`.
- Bugs → open a GitHub issue with the `bug` label and reproduction steps.

## 13. Releasing

- `main` is auto-deployed to staging on merge.
- Production releases are tagged `vYYYY.MM.DD-N` and promoted from staging.
- Hotfixes branch from the production tag, get a `fix/` PR, and are tagged with the next patch number.

---

Thank you for contributing — small, focused, well-tested PRs are the fastest path to merge.
