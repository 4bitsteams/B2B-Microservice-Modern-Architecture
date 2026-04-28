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

# 4. Run tests
dotnet test B2B.sln
```

After step 1, the following are available locally:

| URL | Purpose |
|---|---|
| http://localhost:5000 | Gateway (your entry point) |
| http://localhost:16686 | Jaeger UI |
| http://localhost:5341 | Seq logs |
| http://localhost:8025 | MailHog (catches outgoing email) |
| http://localhost:5050 | pgAdmin |
| http://localhost:15672 | RabbitMQ management (guest/guest unless overridden) |

## 3. Branching & Commits

- `main` is always green and deployable. No direct pushes.
- Branch names: `feat/<topic>`, `fix/<topic>`, `chore/<topic>`, `docs/<topic>`.
- Conventional commits in messages:

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
- **Aggregate-only mutation.** Child entities (e.g. `OrderItem`) are mutated through their root only. No child-only repository.
- **Multi-tenancy.** Every query filters on `ICurrentUser.TenantId`. PRs lacking this filter will be blocked.
- **CQRS purity.** Commands return `Result` / `Result<T>`. Queries return `Result<T>` and have no side-effects.
- **Type aliases.** Use `using OrderEntity = B2B.Order.Domain.Entities.Order;` etc. in all files under `B2B.Order.*` and `B2B.Product.*`.
- **MediatR 12.** Pipeline `RequestHandlerDelegate<TResponse>` takes no parameters: `await next();` not `await next(ct);`.

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
- No unused `using` directives — let the IDE clean them up.

### 4.4 Comments

- Comment **why**, not **what**. The code already says what.
- Don't reference tickets, PRs, or "fixed bug X" in code comments — those belong in commit messages and the PR description.
- Public types and methods on shared abstractions (`B2B.Shared.Core`) require XML doc comments. Service-specific internal types do not.

## 5. Adding a Feature — Walkthrough

### 5.1 Add a Command (e.g. CancelOrder)

1. **Domain** — add `Order.Cancel(reason)` if not present, raising `OrderCancelledEvent`.
2. **Application/Commands/CancelOrder/**
   - `CancelOrderCommand.cs` — `ICommand` (or `ICommand<TResponse>`).
   - `CancelOrderHandler.cs` — `ICommandHandler<CancelOrderCommand>`.
   - `CancelOrderValidator.cs` — `AbstractValidator<CancelOrderCommand>` (auto-registered).
3. **API/Controllers/OrdersController.cs** — add `Cancel(Guid id, [FromBody] CancelOrderRequest)`.
4. **Authorizer (if resource-based)** — if the operation requires ownership or fine-grained permission checks beyond `[Authorize]`, add `{Name}Authorizer.cs : IAuthorizer<{Name}Command>` in the same folder and register it:
   ```csharp
   services.AddScoped<IAuthorizer<CancelOrderCommand>, CancelOrderAuthorizer>();
   ```
5. **Test** — cover the handler (`{Name}HandlerTests.cs`) for the happy path and each `Error.*` branch, plus the authorizer (`{Name}AuthorizerTests.cs`) for all allowed and denied cases.

If the operation is non-naturally-idempotent (creates money-affecting state), make the command implement `IIdempotentCommand` and read `Idempotency-Key` from the header.

### 5.2 Add a Query

Same shape but use `IQuery<TResponse>` / `IQueryHandler<,>`. Queries should hit the cache where reasonable:

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

Integration event contracts belong in `B2B.Shared.Core/Messaging/IntegrationEvents.cs` so both publishers and consumers reference the same types.

## 6. Adding a New Microservice

See [LLD §12](docs/LLD.md#12-adding-a-new-microservice-recipe). Summarised checklist:

- [ ] Four projects (`Domain`, `Application`, `Infrastructure`, `Api`)
- [ ] Reference graph correct (Domain has zero deps)
- [ ] Standard subfolders present
- [ ] Type alias if name collides
- [ ] `AddSharedInfrastructure` + `AddEventBus` wired
- [ ] YARP route + cluster + health check added
- [ ] `docker-compose.yml` + `docker-compose.override.yml` updated
- [ ] `CREATE DATABASE` line in `infrastructure/postgres/init.sql`
- [ ] Test project under `tests/` and added to `B2B.sln`

## 7. Testing

```bash
# Run all tests
dotnet test B2B.sln

# Single project
dotnet test tests/B2B.Order.Tests/B2B.Order.Tests.csproj
dotnet test tests/B2B.Product.Tests/B2B.Product.Tests.csproj
dotnet test tests/B2B.Identity.Tests/B2B.Identity.Tests.csproj
dotnet test tests/B2B.Shared.Tests/B2B.Shared.Tests.csproj

# Single test
dotnet test --filter "FullyQualifiedName~CreateOrderHandlerTests.Confirms_Order_When_Items_Valid"
```

Current test suite (all in `B2B.sln`):

| Project | Tests | Focus |
|---|---|---|
| `B2B.Identity.Tests` | 135 | Domain, application handlers (10), validators, token service |
| `B2B.Product.Tests` | 55 | Domain aggregates, value objects, application handlers, cache behavior |
| `B2B.Order.Tests` | 65 | Domain aggregates, value objects, application handlers, authorizers |
| `B2B.Shared.Tests` | 12 | Pipeline behaviors (Validation, Authorization, Idempotency) |

`B2B.Identity.Tests` breakdown:

| File | Tests | Coverage |
|---|---|---|
| `Domain/UserTests.cs` | ~14 | Aggregate factory, password, role, state transitions |
| `Domain/TenantTests.cs` | ~8 | Tenant factory, slug normalization |
| `Domain/RefreshTokenTests.cs` | ~6 | Token expiry, revocation |
| `Application/LoginHandlerTests.cs` | ~8 | Success, wrong password, inactive user, not found |
| `Application/RegisterUserHandlerTests.cs` | ~8 | Success, email conflict, tenant not found |
| `Application/RefreshTokenHandlerTests.cs` | ~8 | Success, expired, revoked, not found |
| `Application/ChangePasswordHandlerTests.cs` | 6 | Success, wrong password, user not found, no-save on failure |
| `Application/UpdateProfileHandlerTests.cs` | 4 | Success, updates fields, persists, not found |
| `Application/GetUserProfileHandlerTests.cs` | 3 | Returns summary, EmailVerified flag, not found |
| `Application/GetUsersHandlerTests.cs` | 3 | Returns paged list, maps to DTOs, empty page |
| `Application/GetUserByIdHandlerTests.cs` | 3 | Found, not found, cross-tenant isolation |
| `Application/GetTenantsHandlerTests.cs` | 3 | Returns list, maps DTOs, empty |
| `Application/GetTenantBySlugHandlerTests.cs` | 3 | Found, not found, error code/message |
| `Application/Validators/LoginValidatorTests.cs` | ~10 | Field-level validation rules |
| `Application/Validators/RegisterUserValidatorTests.cs` | ~10 | Field-level validation rules |
| `Infrastructure/TokenServiceTests.cs` | ~16 | JWT generation, claims, refresh token rotation |

`B2B.Product.Tests` breakdown:

| File | Tests | Coverage |
|---|---|---|
| `Domain/ProductTests.cs` | ~7 | Aggregate factory, status transitions, stock, domain events |
| `Domain/MoneyTests.cs` | ~6 | Value object equality, arithmetic, currency validation |
| `Domain/CategoryTests.cs` | 6 | Factory, slug normalization, parent ID, Update, Activate/Deactivate |
| `Application/CreateProductHandlerTests.cs` | 6 | Success, persist, category not found, cross-tenant, SKU conflict, concurrent SKU |
| `Application/UpdateProductHandlerTests.cs` | 5 | Success, updates fields, cache invalidation, not found, cross-tenant |
| `Application/AdjustStockHandlerTests.cs` | 5 | Increment/decrement, cache invalidation, not found, cross-tenant |
| `Application/ArchiveProductHandlerTests.cs` | 5 | Success, sets status, persists, not found, cross-tenant |
| `Application/CreateCategoryHandlerTests.cs` | 4 | Success, persist, name conflict, cross-tenant |
| `Application/GetProductByIdHandlerTests.cs` | 5 | Cache hit (no DB call), cache miss, not found, cross-tenant, maps DTO |
| `Application/GetCategoriesHandlerTests.cs` | 3 | Returns paged list, maps DTOs, empty |
| `Application/GetLowStockProductsHandlerTests.cs` | 3 | Returns list, threshold applied, empty |

`B2B.Order.Tests` breakdown:

| File | Tests | Coverage |
|---|---|---|
| `Domain/OrderTests.cs` | 7 | Aggregate invariants, state transitions, totals |
| `Domain/OrderItemTests.cs` | 6 | Factory, TotalPrice, zero/negative invariants, IncrementQuantity |
| `Domain/AddressTests.cs` | 7 | Factory, blank-field invariants, value-object equality, ToString |
| `Application/CreateOrderHandlerTests.cs` | 7 | Happy path, tax rate, item accumulation, persistence |
| `Application/CancelOrderHandlerTests.cs` | 7 | All status branches, tenant isolation, NotFound / Validation errors |
| `Application/ConfirmOrderHandlerTests.cs` | 6 | Pending→Confirmed, persists, not found, cross-tenant, already-confirmed validation |
| `Application/ShipOrderHandlerTests.cs` | 5 | Processing→Shipped, tracking number, persists, not found, wrong-status validation |
| `Application/DeliverOrderHandlerTests.cs` | 5 | Shipped→Delivered, sets DeliveredAt, persists, not found, wrong-status validation |
| `Application/GetOrdersHandlerTests.cs` | 4 | Customer scope, admin scope, DTO mapping, status filter |
| `Application/GetOrderByIdHandlerTests.cs` | 5 | Owner access, TenantAdmin access, not found, cross-tenant, non-owner forbidden |
| `Application/CancelOrderAuthorizerTests.cs` | 6 | Role-based and ownership-based authorization, cross-tenant pass-through |

`B2B.Shared.Tests` breakdown:

| File | Tests | Coverage |
|---|---|---|
| `Behaviors/ValidationBehaviorTests.cs` | 4 | No validators → next, valid → next, invalid → short-circuit, Validation error type |
| `Behaviors/AuthorizationBehaviorTests.cs` | 4 | No authorizers → next, success → next, fail → short-circuit, Forbidden error type/code |
| `Behaviors/IdempotencyBehaviorTests.cs` | 4 | Blank key → no cache, first call → caches, duplicate → cached result, failure → not cached |

Targets:

- Domain: ≥ 90 % line coverage on aggregates and value objects.
- Application: every handler has at least one success test and one test per `Error.*` branch it can return.
- Authorizers: every `IAuthorizer<T>` implementation has tests for all success paths and each failure path.
- Infrastructure: integration tests with Testcontainers if behavior depends on the real DB or broker.

## 8. Observability During Development

- **Watching logs in real time:** open Seq at http://localhost:5341.
- **Tracing a request end-to-end:** open Jaeger at http://localhost:16686, search the service name, find the trace by `traceId` (Serilog logs include it).
- **Inspecting RabbitMQ queues:** http://localhost:15672 → Queues tab → check message rate, ready/unacked counts.
- **Reading email:** http://localhost:8025 (MailHog catches everything).

## 9. Database Migrations

```bash
# From the service's Infrastructure project directory:
dotnet ef migrations add MyMigration --startup-project ../B2B.Order.Api
dotnet ef database update --startup-project ../B2B.Order.Api
```

Migrations live under `Persistence/Migrations/`. Squash only with explicit team approval.

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
- [ ] Tests added/updated and passing locally
- [ ] No new business exception thrown — used Result + Error
- [ ] All queries scoped by TenantId
- [ ] No direct reference to BCrypt.Net / Npgsql / Redis / MassTransit from Application or Domain
- [ ] Type alias used in files under namespaces with name collision
- [ ] Authorization logic extracted to `IAuthorizer<TCommand>` (not embedded in handler)
- [ ] One public type per file
- [ ] Public API change reflected in the relevant doc (HLD/LLD/BRD)
- [ ] New test project added to B2B.sln if applicable
```

## 11. Security & Secrets

- **Never** commit secrets. Local defaults in `docker-compose.override.yml` are for local Mailhog/RabbitMQ only.
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
