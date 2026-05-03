# B2B Microservice Platform — Technical Presentation

---

## Slide 1 — Title

```
╔══════════════════════════════════════════════════════════════════╗
║                                                                  ║
║         B2B Microservice Modern Architecture                     ║
║                                                                  ║
║   Production-grade multi-tenant commerce backbone               ║
║   Built for millions of concurrent users                        ║
║                                                                  ║
║   ASP.NET Core 9 · Clean Architecture · CQRS · DDD              ║
║   Event-Driven · Polly · HybridCache · OpenTelemetry            ║
║                                                                  ║
║   586 tests · 0 failures · Docker-compose ready in 2 min        ║
║                                                                  ║
╚══════════════════════════════════════════════════════════════════╝
```

**Audience:** Engineering teams, architects, technical stakeholders
**Date:** 2026-05-03

---

## Slide 2 — Problem Statement

### What We Are Solving

| Challenge | Business Pain |
|---|---|
| Duplicate orders under flaky networks | Refund cost + trust loss |
| Tenant data leaking across boundaries | Enterprise compliance failure |
| A single hot tenant degrading all others | SLA breach for other tenants |
| Integration failures leaving orphan saga state | Data inconsistency, manual cleanup |
| No visibility into what broke and when | Slow incident recovery |
| Unscalable DB connections as replicas grow | Connection exhaustion under load |
| Hard-coded cross-cutting concerns in handlers | Impossible to test, impossible to extend |

### What We Deliver

> A production-grade, extensible B2B commerce backbone that any future business module can plug into without re-deciding cross-cutting concerns.

---

## Slide 3 — Business Goals

| # | Goal | Mechanism |
|---|---|---|
| G1 | Onboard a new tenant in **minutes** | Multi-tenant JWT, row-level isolation, one `docker compose up` |
| G2 | Process orders **idempotently** | 24h idempotency cache, Redis-backed, pipeline-level |
| G3 | Per-tenant **data isolation** | Global EF query filter on `ITenantEntity`; automatic, not per-handler |
| G4 | Full **audit & traceability** | Audit behavior, W3C trace propagation end-to-end |
| G5 | **Independent service evolution** | Database-per-service, async contracts, bounded contexts |
| G6 | **Predictable cost** at 10× load | HybridCache, PgBouncer, output caching, bulkhead isolation |
| G7 | Vendor **self-onboarding** | Approval workflow saga, role-based authorization |
| G8 | Repeat purchase through **discounts** | Coupon engine, percentage / fixed / free-shipping rules |

---

## Slide 4 — Platform at a Glance

### 10 Services + 1 Worker

```
                         ┌─────────────────────┐
   Buyers ──────────────▶│   YARP Gateway :5000 │◀────── Partner APIs
   Admins ──────────────▶│  JWT · Rate-limit    │
   Vendors ─────────────▶│  Health checks       │
                         └──────────┬──────────┘
                                    │
          ┌──────┬──────┬──────┬────┴───┬──────┬──────┬──────┬──────┐
          ▼      ▼      ▼      ▼        ▼      ▼      ▼      ▼      ▼
        :5001  :5002  :5003  :5004    :5005  :5006  :5007  :5008  :5011
       Identity Product Order Basket Payment Shipping Vendor Discount Review
          │      │      │      │        │      │      │      │      │
          └──────┴──────┴──────┴────────┴──────┴──────┴──────┴──────┘
                                    │
                           Apache Kafka :9092
                          (MassTransit Kafka Rider)
                                    │
                         Notification Worker ──▶ MailHog / SMTP
```

### Infrastructure Stack

| Service | Technology | Port |
|---|---|---|
| Gateway | YARP 2.2 + Rate Limiter | 5000 |
| Services (×9) | ASP.NET Core 9 | 5001–5011 |
| Database | PostgreSQL 16 (one DB per service) | 5432 |
| Connection pool | PgBouncer (transaction mode) | 6432 |
| Cache L1 | HybridCache (in-process, 2 min) | — |
| Cache L2 | Redis 7 | 6379 |
| Messaging | Apache Kafka 3.7 KRaft + MassTransit 8.3 | 9092 |
| Kafka UI | provectuslabs/kafka-ui | 8090 |
| Traces | OpenTelemetry → OTel Collector → Jaeger | 16686 |
| Metrics | OpenTelemetry → OTel Collector → Prometheus → Grafana | 9090 / 3000 |
| Logs | Serilog → Seq | 5341 |
| Email (dev) | MailHog | 8025 |

---

## Slide 5 — Architectural Pillars

### 1. Clean Architecture (per service)

```
Domain          ──▶  no dependencies
Application     ──▶  Domain + Shared.Core abstractions only
Infrastructure  ──▶  Application + Shared.Infrastructure
API             ──▶  Infrastructure (wires DI, no business logic)
```

**Rule:** Arrows always point inward. A reverse reference is a build break, not a code-review nit.

### 2. CQRS via MediatR 12

```csharp
// Command — mutates state, returns Result<TResponse>
public sealed record CreateOrderCommand(AddressDto ShippingAddress, List<OrderItemDto> Items)
    : ICommand<CreateOrderResponse>, IIdempotentCommand;

// Query — reads state, no side effects
public sealed record GetOrdersQuery(int Page, int PageSize)
    : IQuery<PagedList<OrderSummaryDto>>;
```

### 3. Result Pattern — no business exceptions

```csharp
return Error.NotFound("Order.NotFound", $"Order {id} not found.");       // → 404
return Error.Validation("Basket.Empty", "Basket has no items.");          // → 400
return Error.Conflict("Vendor.TaxIdExists", "Tax ID already registered."); // → 409
return Error.ServiceUnavailable("Bulkhead.Full", "Try again shortly.");   // → 503
```

### 4. Domain-Driven Design

- `AggregateRoot<TId>` raises domain events; `DomainEventBehavior` publishes after `SaveChangesAsync`
- Value objects (`Address`, `Money`) enforce invariants via static factories
- Child entities mutate **only** through their aggregate root

### 5. Event-Driven Messaging

- **Domain events** — in-process via MediatR
- **Integration events** — cross-service via Apache Kafka (MassTransit Kafka Rider); producer per event type, consumer groups per service

---

## Slide 6 — MediatR Pipeline

### Request flows through 8 behaviors before reaching the handler

```
Request
  ➜ LoggingBehavior          ← request name + elapsed ms
    ➜ RetryBehavior           ← Bulkhead → Circuit Breaker → Retry (Polly 8)
      ➜ IdempotencyBehavior   ← 24h Redis cache (IIdempotentCommand only)
        ➜ PerformanceBehavior ← warning when handler exceeds threshold
          ➜ AuthorizationBehavior ← role-based, IAuthorizer<TCommand>
            ➜ ValidationBehavior  ← FluentValidation, short-circuits on error
              ➜ AuditBehavior     ← command metadata → audit log
                ➜ DomainEventBehavior ← drains + publishes after SaveChangesAsync
                  ➜ Handler
```

### What this buys you

| Benefit | Detail |
|---|---|
| **Open/Closed** | New cross-cutting concern = new behavior file; existing handlers untouched |
| **Testable isolation** | Each behavior tested independently with NSubstitute mocks |
| **Consistent observability** | Every request logged, every slow handler flagged, every audit written |
| **Fail-safe** | Bulkhead/circuit breaker protect downstream before handler is even called |

---

## Slide 7 — Resilience: Three-Layer Polly 8 Pipeline

### The problem

A downstream dependency (DB, Redis, broker) becomes slow or unavailable. Without protection:
- Thread pool exhaustion → entire process becomes unresponsive
- One bad dependency cascades to all commands
- Retry storms amplify the problem

### The solution

```
CommandBulkheadProvider (singleton ConcurrentDictionary<Type, SemaphoreSlim>)
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│  Layer 1: BULKHEAD                                          │
│  SemaphoreSlim per command type                             │
│  WaitAsync(0) — non-blocking, rejects instantly             │
│  → returns Error.ServiceUnavailable (HTTP 503)              │
├─────────────────────────────────────────────────────────────┤
│  Layer 2: CIRCUIT BREAKER                                   │
│  Opens when failure ratio ≥ 50% over 10s window (min 5)    │
│  Stays open for 30s; logs OPEN / HALF-OPEN / CLOSED        │
│  → returns Error.ServiceUnavailable (HTTP 503)              │
├─────────────────────────────────────────────────────────────┤
│  Layer 3: RETRY                                             │
│  Exponential back-off + jitter; max 3 attempts              │
│  Retries transient exceptions only; never retries 4xx       │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
      next() → Handler
```

### Configuration (appsettings.json)

```json
"RetryBehavior": {
  "MaxRetryAttempts": 3,
  "CircuitBreakerFailureRatio": 0.5,
  "CircuitBreakerSamplingDurationSeconds": 10,
  "CircuitBreakerBreakDurationSeconds": 30,
  "BulkheadMaxConcurrency": 100
}
```

---

## Slide 8 — Caching: Two Layers

### The problem at scale

A product list page under 50,000 concurrent users → cache miss storms → DB overload → latency spike.

### Layer 1 — HybridCache (L1 + L2, stampede-safe)

```csharp
// Query handler — zero Redis round-trips for hot keys (served from L1 in-process)
public async Task<Result<PagedList<ProductDto>>> Handle(GetProductsQuery q, CancellationToken ct)
{
    return await hybridCache.GetOrCreateAsync(
        $"products:tenant:{tenantId}:page:{q.Page}",
        async cancel => await repo.GetPagedAsync(..., cancel),
        cancellationToken: ct);
}
```

| | L1 | L2 |
|---|---|---|
| Store | In-process memory | Redis 7 |
| TTL | 2 minutes | 15 minutes |
| Stampede protection | Built-in coalescing — only **one** thread calls the factory | — |
| Scope | Per replica | Shared across all replicas |

### Layer 2 — ICacheService (fine-grained control)

```csharp
// Cache-aside with prefix-based invalidation
await cache.RemoveByPrefixAsync($"products:tenant:{tenantId}");
```

- Used for scenarios requiring custom TTLs or targeted key invalidation
- `RedisCacheService` swallows transient failures → degrades to direct DB hit

---

## Slide 9 — Multi-Tenancy

### Architecture

Every entity stores `TenantId`. Automatic isolation at the database layer — no per-handler filter code.

```csharp
// ITenantEntity marker — implemented by all tenant-scoped aggregates
public interface ITenantEntity { Guid TenantId { get; } }

// Order, Product, User, Payment, Shipment, Vendor, Discount, Review all implement it

// BaseDbContext applies the filter automatically via reflection
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes()
        .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)))
    {
        ApplyTenantFilter(entityType, modelBuilder);  // HasQueryFilter(e => e.TenantId == CurrentTenantId)
    }
}
```

### What this means

| Scenario | Behaviour |
|---|---|
| Normal HTTP request | `ICurrentUser.TenantId` from JWT; filter applied transparently |
| Background service (`StuckSagaCleanupWorker`) | Calls `IgnoreQueryFilters()` explicitly to scan all tenants |
| Unit test with `ICurrentUser` mock | Returns test `TenantId`; filter scopes to that tenant |
| Forgot to implement `ITenantEntity` | Data is **not** filtered — code review checklist catches this |

### Defense-in-depth

1. Gateway validates JWT signature, issuer, audience, lifetime
2. Each service re-validates the JWT
3. `AuthorizationBehavior` enforces role-based access before handler runs
4. Global EF filter prevents cross-tenant data access at the ORM layer

---

## Slide 10 — Order Fulfillment Saga

### The business flow

```
Buyer places order
      │
      ▼
  OrderConfirmed ──────────────────────────────────────────────┐
      │                                                         │
      ▼                                                         │
 AwaitingStockReservation                                       │
  ├── StockReserved ──────────────────────────────────────┐    │
  └── StockReservationFailed / timeout ──────────────────▶│    │
      │ Compensate: OrderCancelledDueToStock               │    │
      │                                                    │    │
      ▼                                                    │    │
 AwaitingPayment                                           │    │
  ├── PaymentProcessed ──────────────────────────────┐    │    │
  └── PaymentFailed / timeout ──────────────────────▶│    │    │
      │ Compensate: ReleaseStock + CancelOrder         │    │    │
      │                                               │    │    │
      ▼                                               │    │    │
 AwaitingShipment                                     │    │    │
  ├── ShipmentCreated ──────────────────────────┐     │    │    │
  └── ShipmentFailed / timeout ────────────────▶│     │    │    │
      │ Compensate: RefundPayment + ReleaseStock  │    │    │    │
      │                                          │    │    │    │
      ▼                                          ▼    ▼    ▼    ▼
  OrderFulfilled ─────────────────────────────────── Final ◀───┘
```

### Saga guarantees

| Guarantee | How |
|---|---|
| **At-least-once delivery** | MassTransit in-memory outbox; retry on consumer failure |
| **Optimistic concurrency** | `ConcurrencyMode.Optimistic` on saga EF rows |
| **Timeout-driven compensation** | In-memory bus built-in scheduler; saga auto-cancels |
| **Stuck saga detection** | `StuckSagaCleanupWorker` scans every 5 min; logs `Warning` |
| **State persistence** | Saga state in `b2b_order` DB; survives service restarts |

---

## Slide 11 — Event-Driven Architecture

### Signal types

| Type | Transport | Example | Scope |
|---|---|---|---|
| Domain Event | MediatR in-process | `OrderConfirmedEvent` | Within one service |
| Integration Event | Apache Kafka (topic) | `OrderConfirmedIntegration` | Cross-service |
| Saga Command | In-memory bus (typed) | `ReserveStockCommand` | Orchestrator → participant |

### Integration event contracts (shared, versioned)

All contracts live in `B2B.Shared.Core/Messaging/IntegrationEvents.cs`:

```csharp
public sealed record OrderConfirmedIntegration(Guid OrderId, string OrderNumber, Guid CustomerId, ...);
public sealed record BasketCheckedOutIntegration(Guid UserId, Guid TenantId, List<BasketItemDto> Items, ...);
public sealed record UserRegisteredIntegration(Guid UserId, string Email, ...);
public sealed record ProductLowStockIntegration(Guid ProductId, string Sku, int CurrentStock, ...);
```

### Notification Worker — 9 consumer types

| Event consumed | Email sent |
|---|---|
| `OrderConfirmedIntegration` | Order confirmation |
| `OrderProcessingStartedIntegration` | Processing started |
| `OrderFulfilledIntegration` | Delivery confirmation |
| `OrderCancelledDueToStockIntegration` | Stock failure notification |
| `OrderCancelledDueToPaymentIntegration` | Payment failure notification |
| `OrderCancelledDueToShipmentIntegration` | Shipment failure notification |
| `UserRegisteredIntegration` | Welcome email |
| `ProductLowStockIntegration` | Low-stock alert to tenant admin |
| *Failed delivery* | Retried with exponential backoff before parking |

---

## Slide 12 — Scalability at a Glance

### How we handle millions of concurrent users

```
                   ┌─────────────────────────────────────┐
                   │          YARP Gateway :5000          │
                   │  Per-tenant sliding window (1000/min)│
                   │  Partitioned: X-Tenant-ID → JWT → IP│
                   └──────────────────┬──────────────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              │                       │                       │
     ┌────────▼─────────┐  ┌─────────▼────────┐  ┌──────────▼────────┐
     │   Service Pod 1  │  │   Service Pod 2   │  │   Service Pod N   │
     │                  │  │                  │  │                   │
     │  L1 HybridCache  │  │  L1 HybridCache  │  │  L1 HybridCache   │
     │  (in-process)    │  │  (in-process)    │  │  (in-process)     │
     │  Bulkhead: 100   │  │  Bulkhead: 100   │  │  Bulkhead: 100    │
     │  concurrent/type │  │  concurrent/type │  │  concurrent/type  │
     └────────┬─────────┘  └─────────┬────────┘  └──────────┬────────┘
              └───────────────────────┴───────────────────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              │                       │                       │
     ┌────────▼─────────┐  ┌─────────▼────────┐  ┌──────────▼────────┐
     │   Redis (L2)     │  │   PgBouncer :6432 │  │  Apache Kafka     │
     │  15 min cache    │  │  5,000 clients    │  │  :9092            │
     │  Shared L2       │  │  → 200 PG conns   │  │  Consumer group   │
     └──────────────────┘  └─────────┬────────┘  └───────────────────┘
                                      │
                            ┌─────────▼────────┐
                            │  PostgreSQL :5432 │
                            │  (one DB/service) │
                            └──────────────────┘
```

### Scaling dimensions

| Axis | Approach | Handles |
|---|---|---|
| Read hotspots | HybridCache L1 (in-process, stampede-safe) | Hot product pages, repeated queries |
| Shared read cache | Redis L2 (15 min) | Cross-replica cache misses |
| Write throughput | Horizontal pod scale-out; stateless APIs | More concurrent writers |
| DB connections | PgBouncer transaction pool (5,000 → 200) | High replica count without connection exhaustion |
| Hot tenant | Per-tenant gateway rate limit (1,000 req/min) | Noisy-neighbour isolation |
| Concurrency overload | Bulkhead per command type (100 concurrent) | Prevents thread pool exhaustion |
| Dependency failure | Circuit breaker (breaks at 50% failure) | Prevents cascade to healthy services |
| Worker throughput | Kafka consumer groups (partition-parallel) | Message processing scale-out |
| Response bandwidth | Brotli/Gzip compression + output caching (30s) | Reduced payload + repeated list queries |

---

## Slide 13 — Security Architecture

### Layers of defense

```
┌─────────────────────────────────────────────────────────┐
│  Layer 1: Gateway                                        │
│  • JWT signature, issuer, audience, lifetime validation  │
│  • Per-tenant + per-IP rate limiting                     │
│  • Health-gated routing (unhealthy pods removed)         │
├─────────────────────────────────────────────────────────┤
│  Layer 2: Service                                        │
│  • Re-validates JWT (defence-in-depth)                   │
│  • ICurrentUser resolved from claims                     │
├─────────────────────────────────────────────────────────┤
│  Layer 3: MediatR Pipeline                               │
│  • AuthorizationBehavior: role-based before handler      │
│  • IAuthorizer<TCommand>: resource-based (ownership)     │
├─────────────────────────────────────────────────────────┤
│  Layer 4: Data                                           │
│  • Global EF filter: TenantId on every ITenantEntity     │
│  • PK: client-side Guid (no sequential int exposure)     │
│  • BCrypt password hashing (cost factor configurable)    │
├─────────────────────────────────────────────────────────┤
│  Layer 5: Tokens                                         │
│  • JWT TTL: 60 min                                       │
│  • Refresh tokens: max 5 concurrent; rotate on use       │
│  • All revoked on logout                                 │
├─────────────────────────────────────────────────────────┤
│  Layer 6: Secrets                                        │
│  • All credentials from environment variables            │
│  • Defaults only in docker-compose (local dev)           │
│  • Never committed to source control                     │
└─────────────────────────────────────────────────────────┘
```

---

## Slide 14 — Observability

### Three pillars, fully implemented

```
Every inbound request
        │
        ├── W3C Trace Context (propagates through HTTP + RabbitMQ headers)
        │         │
        │         ▼
        │   OTel Collector :4317
        │         │
        │         ├──────────────────▶ Jaeger :16686  (distributed traces)
        │         │
        │         └──────────────────▶ Prometheus :9090 ──▶ Grafana :3000
        │                                                     (RED metrics dashboard)
        │
        ├── Structured JSON logs
        │         │
        │         └──────────────────▶ Seq :5341
        │                              (full-text search, alert rules)
        │
        └── Health endpoints
                  ├── /health/live   → always 200 (liveness probe)
                  └── /health/ready  → DB + Redis (readiness probe)
```

### Instrumentation coverage

| Signal | Sources |
|---|---|
| Traces | ASP.NET Core, HttpClient, EF Core, Kafka (MassTransit Rider) |
| Metrics | .NET Runtime (GC, thread pool, allocations), EF Core, ASP.NET Core |
| Logs | `LoggingBehavior` (every request), `RetryBehavior` (circuit transitions), `StuckSagaCleanupWorker` |

### MediatR Pipeline log entries

```
INF  Handled CreateOrderCommand in 42ms
WRN  Handled GetProductsQuery in 312ms [exceeds 200ms threshold]
WRN  Circuit breaker OPEN for CreatePaymentCommand — failure ratio 0.62 over 10s
WRN  Stuck saga detected: OrderId=abc State=AwaitingPayment Age=32.4 min
```

---

## Slide 15 — Service Catalogue

| Service | Port | DB | Key Domain Concepts | Notable Features |
|---|---|---|---|---|
| **Identity** | 5001 | `b2b_identity` | User, Tenant, RefreshToken | BCrypt, JWT 60min TTL, refresh rotation (max 5 active) |
| **Product** | 5002 | `b2b_product` | Product, Category | Low-stock events, HybridCache read path, stock reservation saga participant |
| **Order** | 5003 | `b2b_order` | Order, OrderItem | Idempotency key, fulfillment saga orchestrator, `StuckSagaCleanupWorker` |
| **Basket** | 5004 | Redis only | Basket, BasketItem | Ephemeral, Redis hash per user, coupon apply, checkout integration event |
| **Payment** | 5005 | `b2b_payment` | Payment, Invoice | Invoice lifecycle, refund, saga participant |
| **Shipping** | 5006 | `b2b_shipping` | Shipment | Carrier tracking, dispatch, delivery, saga participant |
| **Vendor** | 5007 | `b2b_vendor` | Vendor | Approval workflow, commission rate, suspend/reactivate/deactivate |
| **Discount** | 5008 | `b2b_discount` | Discount, Coupon | Percentage/Fixed/FreeShipping rules, usage limits, expiry |
| **Review** | 5011 | `b2b_review` | Review | Submit → Pending → Approved/Rejected; one review per buyer per product |
| **Notification Worker** | — | — | — | 9 consumer types, SMTP, exponential retry before dead-letter |

---

## Slide 16 — Design Patterns Applied

### Creational

| Pattern | Where | Purpose |
|---|---|---|
| Factory Method | `Order.Create()`, `Product.Create()`, `Address.Create()` | Enforce invariants; no direct `new` |

### Structural

| Pattern | Where | Purpose |
|---|---|---|
| Decorator | MediatR pipeline behaviors (8 layers) | Cross-cutting concerns without touching handlers |
| Facade | `AddSharedInfrastructure()`, `AddEventBus()` | Hide complex DI wiring behind one call |
| Proxy | YARP Gateway | Transparent reverse proxy with JWT + rate limiting |

### Behavioral

| Pattern | Where | Purpose |
|---|---|---|
| Mediator | MediatR `ISender` | Controllers have zero knowledge of handler implementations |
| Chain of Responsibility | MediatR pipeline | Each behavior decides to pass or short-circuit |
| Observer | Domain Events + MassTransit consumers | Aggregates raise events; consumers react without coupling |
| Strategy | `IPasswordHasher`, `ICacheService`, `IEmailService` | Swap algorithm/provider at DI registration |
| Repository | `IOrderRepository`, `IProductRepository` | Abstract persistence as a collection |
| Unit of Work | `IUnitOfWork` / `BaseDbContext` | Single atomic commit across aggregates |
| Outbox | MassTransit in-memory outbox | At-least-once integration event delivery |
| Cache-Aside | `ICacheService.GetOrCreateAsync` + `HybridCache` | Load from cache; fall back to DB; repopulate on miss |
| Bulkhead | `CommandBulkheadProvider` | Isolate concurrency by command type |
| Circuit Breaker | Polly 8 `CircuitBreakerStrategyOptions` | Fail-fast; prevent cascade failure |
| State Machine | `OrderFulfillmentSaga` (MassTransit) | Long-running workflow with compensating rollback |

---

## Slide 17 — SOLID in Practice

### S — Single Responsibility

```
CreateOrderHandler      → order creation only
ValidationBehavior      → validation only
BcryptPasswordHasher    → hashing only
RedisCacheService       → cache read/write only
StuckSagaCleanupWorker  → saga monitoring only
```

### O — Open/Closed

Adding a new cross-cutting concern (e.g. idempotency) = **one new behavior file**. All 40+ existing handlers untouched.

### L — Liskov Substitution

```csharp
// Swap cache backends transparently — handlers never know
ICacheService → RedisCacheService   (production)
ICacheService → MemoryCacheService  (tests, Testcontainers)
```

### I — Interface Segregation

9 narrow port interfaces (`IRepository`, `IUnitOfWork`, `ICacheService`, `IEventBus`, `ICurrentUser`, `IPasswordHasher`, `IEmailService`, `IPaymentGateway`, `IShipmentGateway`) — each handler injects only what it needs.

### D — Dependency Inversion

```
Domain        → no dependencies
Application   → B2B.Shared.Core interfaces only
Infrastructure → implements Core interfaces (EF, Redis, BCrypt, SMTP)
API           → wires DI; depends on Infrastructure for registration only
```

---

## Slide 18 — Testing Strategy

### 586 tests, 0 failures

| Project | Tests | Coverage Focus |
|---|---|---|
| `B2B.Identity.Tests` | 135 | Domain, 10 handler files, validators, token service |
| `B2B.Product.Tests` | 55 | Domain aggregates, value objects, handler + cache behavior |
| `B2B.Order.Tests` | 69 | Domain, handlers, authorizers |
| `B2B.Shared.Tests` | 12 | Pipeline behaviors (Validation, Authorization, Idempotency) |
| `B2B.Basket.Tests` | 52 | Domain, 6 handler files, validators |
| `B2B.Payment.Tests` | 70 | Domain (payment + invoice), 9 handler files, validators |
| `B2B.Shipping.Tests` | 23 | Domain state transitions, handlers |
| `B2B.Discount.Tests` | 76 | Domain (discount + coupon), 7 handler files, validators |
| `B2B.Review.Tests` | 35 | Domain, 6 handler files, validators |
| `B2B.Vendor.Tests` | 59 | Domain lifecycle, 7 handler files, validators |
| **Total** | **586** | **All green** |

### Toolchain

| Layer | Tools |
|---|---|
| Unit | xUnit + FluentAssertions + NSubstitute + Bogus |
| Integration | Testcontainers (real PostgreSQL / Redis / Kafka) |
| API | `WebApplicationFactory<T>` (planned) |
| Validation | FluentValidation.TestHelper |

### Handler test pattern

```csharp
public sealed class CreateOrderHandlerTests
{
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _uow            = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser   = Substitute.For<ICurrentUser>();
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _currentUser.TenantId.Returns(Guid.NewGuid());
        _handler = new CreateOrderHandler(_orderRepo, _uow, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateOrder() { ... }

    [Fact]
    public async Task Handle_EmptyItems_ShouldReturnValidationError() { ... }
}
```

---

## Slide 19 — Developer Experience

### From clone to running system in under 2 minutes

```bash
git clone <repo>
cd "B2B Microservice Modern Architecture"

docker compose up -d          # Postgres, Redis, Kafka, Kafka UI, Jaeger, Seq,
                              # MailHog, pgAdmin, PgBouncer,
                              # OTel Collector, Prometheus, Grafana

dotnet build B2B.sln          # 0 errors

dotnet run --project src/Services/Identity/B2B.Identity.Api
dotnet run --project src/Services/Product/B2B.Product.Api
dotnet run --project src/Services/Order/B2B.Order.Api
dotnet run --project src/Gateway/B2B.Gateway

dotnet test B2B.sln           # 586 tests, 0 failures
```

### Local tooling URLs

| URL | Purpose |
|---|---|
| http://localhost:5000 | Gateway — your API entry point |
| http://localhost:16686 | Jaeger — trace any request end-to-end |
| http://localhost:5341 | Seq — structured logs + alerting |
| http://localhost:3000 | Grafana — RED metrics dashboards |
| http://localhost:9090 | Prometheus — raw metrics |
| http://localhost:8090 | Kafka UI — topics, consumer groups, offsets |
| http://localhost:8025 | MailHog — catch all outgoing email |
| http://localhost:5050 | pgAdmin — Postgres browser |

### Adding a new microservice — 9 steps

1. Create four projects: `Domain`, `Application`, `Infrastructure`, `Api`
2. Follow the dependency rule (Domain has zero deps)
3. Add type alias if name collides with namespace
4. Wire DI: `AddSharedInfrastructure` + `AddEventBus`
5. Add entities — implement `ITenantEntity` for automatic tenant filter
6. Add YARP route + cluster + health check to `B2B.Gateway/appsettings.json`
7. Add service to `docker-compose.yml` + `docker-compose.override.yml`
8. Add `CREATE DATABASE b2b_{svc};` to `infrastructure/postgres/init.sql`
9. Add test project under `tests/` and register in `B2B.sln`

---

## Slide 20 — Non-Functional Requirements Status

| ID | Requirement | Target | Status |
|---|---|---|---|
| NFR-1 | p95 read latency at gateway | < 250 ms | HybridCache L1 (in-process) eliminates DB for hot keys |
| NFR-2 | p95 order-create latency | < 600 ms | Async saga; idempotency fast-path from Redis |
| NFR-3 | Steady-state orders/sec per replica | ≥ 100 | Bulkhead (100 concurrent) + PgBouncer (200 PG conns) |
| NFR-4 | Per-service monthly availability | ≥ 99.5% | Circuit breaker + health-gated routing + liveness probes |
| NFR-5 | No order lost if service crashes mid-write | Hard | Outbox + Npgsql retry-on-failure + saga state in DB |
| NFR-6 | All inter-service ingress authenticated by JWT | Hard | Gateway + per-service re-validation |
| NFR-7 | Secrets from environment, never committed | Hard | `docker-compose.override.yml` only; prod via orchestrator |
| NFR-8 | One tenant cannot access another's data | Hard | Global EF filter on `ITenantEntity` |
| NFR-9 | Every request gets a trace id end-to-end | Hard | W3C TraceContext through HTTP + Kafka message headers |
| NFR-10 | Structured JSON logs to central sink | Hard | Serilog → Seq |
| NFR-11 | Duplicate POST idempotent within 24h | 24h window | `IdempotencyBehavior` + Redis |
| NFR-12 | Auditable order state transitions | ≥ 7 years | `AuditBehavior` + domain events |
| NFR-13 | RPO ≤ 5 min, RTO ≤ 30 min for order datastore | Disaster | PG with `EnableRetryOnFailure`; saga state persistent |

---

## Slide 21 — Roadmap

### Completed

| Item | Impact |
|---|---|
| ✅ Per-tenant rate limiting at gateway | Noisy-neighbour isolation |
| ✅ Global EF query filter (`ITenantEntity`) | Zero per-handler filter boilerplate |
| ✅ HybridCache stampede protection | Hot-key correctness under massive load |
| ✅ Three-layer Polly resilience pipeline | Bulkhead + CB + Retry; HTTP 503 on saturation |
| ✅ PgBouncer transaction-mode pool | Connection scaling to 5,000 clients |
| ✅ OpenTelemetry metrics → Prometheus → Grafana | RED metrics dashboards |
| ✅ OTel Collector fan-out (traces → Jaeger, metrics → Prometheus) | Single exporter per service |
| ✅ Split `/health/live` + `/health/ready` | Proper Kubernetes liveness/readiness |
| ✅ `StuckSagaCleanupWorker` | Stuck saga detection and alerting |
| ✅ Brotli/Gzip response compression + output caching | Bandwidth and DB load reduction |
| ✅ Apache Kafka migration (MassTransit Kafka Rider, KRaft) | Durable log, consumer groups, replay capability |

### Next priorities

| Priority | Item | Why |
|---|---|---|
| P0 | Persistent EF outbox replacing in-memory | Survive process crash without losing integration events |
| P0 | Optimistic concurrency (`xmin`) + `ConcurrencyBehavior` | Correct under concurrent writes to aggregates |
| P1 | `WebApplicationFactory<T>` API tests | Full-stack test coverage including routing + auth |
| P2 | Full-text search (Elasticsearch / Meilisearch) | Once catalog volume justifies a dedicated read store |
| P2 | Real payment gateway (Stripe / Adyen) | PCI-scope billing service |
| P3 | Multi-region active-active | When SLAs require geographic redundancy |

---

## Slide 22 — Key Architectural Decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | Database-per-service | Independent schema evolution; migration blast radius isolated |
| 2 | Async-first cross-service contracts | No latency stacking; services are autonomous |
| 3 | Result pattern over exceptions | Errors are part of the contract; exceptions are for bugs |
| 4 | MediatR pipeline behaviors | Cross-cutting at the right abstraction; handlers stay pure |
| 5 | YARP over Ocelot / NGINX | First-party .NET; extendable in C# |
| 6 | MassTransit + Apache Kafka | Durable log, consumer groups, partition-parallel processing, replay; KRaft = no ZooKeeper |
| 7 | Orchestration (saga) over choreography | Explicit state machine; compensation logic in one place |
| 8 | Integration contracts in `B2B.Shared.Core` | Single canonical source; publishers and consumers aligned |
| 9 | Redis-only for Basket | Intentionally ephemeral; eliminates migration overhead |
| 10 | Type alias convention | `Order`, `Product`, `Vendor` are namespace and entity — alias prevents `CS0118` |
| 11 | HybridCache over pure Redis | L1 in-process eliminates network for hot keys; built-in stampede protection |
| 12 | Bulkhead via `SemaphoreSlim` (not Polly.RateLimiting) | `Polly.RateLimiting` requires `net7.0` TFM; custom bulkhead is portable |
| 13 | `ITenantEntity` global filter | Eliminates per-handler `Where(TenantId ==...)` — can't be forgotten |
| 14 | PgBouncer transaction mode | Compatible with horizontal scale-out; respects EF Core prepared-statement constraints |

---

## Slide 23 — Summary

### What makes this architecture production-grade

```
┌─────────────────────────────────────────────────────────────────┐
│                    BUSINESS OUTCOMES                            │
│  ✓ New tenant onboarded in minutes                             │
│  ✓ Idempotent orders — no duplicate charges                    │
│  ✓ Saga compensation — no orphan state                         │
│  ✓ Independent service deploys                                  │
└──────────────────────────────┬──────────────────────────────────┘
                               │ enabled by
┌──────────────────────────────▼──────────────────────────────────┐
│                 ARCHITECTURAL DECISIONS                         │
│  Clean Architecture · CQRS · DDD · Event-Driven · Saga         │
│  Result Pattern · Repository · Unit of Work · Outbox           │
└──────────────────────────────┬──────────────────────────────────┘
                               │ implemented with
┌──────────────────────────────▼──────────────────────────────────┐
│                   SCALABILITY LEVERS                            │
│  HybridCache (L1+L2) · PgBouncer · Bulkhead · Circuit Breaker  │
│  Per-tenant rate limiting · Output caching · Compression        │
└──────────────────────────────┬──────────────────────────────────┘
                               │ observable through
┌──────────────────────────────▼──────────────────────────────────┐
│                    OBSERVABILITY                                │
│  Traces → Jaeger  ·  Metrics → Grafana  ·  Logs → Seq         │
│  Split health probes  ·  Stuck saga alerting                   │
└──────────────────────────────┬──────────────────────────────────┘
                               │ verified by
┌──────────────────────────────▼──────────────────────────────────┐
│                    TEST SUITE                                   │
│              586 tests · 0 failures                            │
│  Domain · Handlers · Authorizers · Validators · Behaviors      │
└─────────────────────────────────────────────────────────────────┘
```

### Technology snapshot

**.NET 9** · **ASP.NET Core 9** · **EF Core 9** · **MediatR 12** · **Polly 8** · **MassTransit 8.3** · **Apache Kafka 3.7** · **PostgreSQL 16** · **PgBouncer** · **Redis 7** · **HybridCache** · **YARP 2.2** · **OpenTelemetry** · **Prometheus** · **Grafana** · **Jaeger** · **Seq** · **Docker**

---

*Document type: Technical Presentation*
*Platform: B2B Microservice Modern Architecture*
*Last revised: 2026-05-03*
*Tests: 586 · Failures: 0*
