# Business Requirements Document — B2B Microservice Platform

| Field | Value |
|---|---|
| Document type | Business Requirements Document (BRD) |
| Status | Living document |
| Owner | Platform Engineering |
| Last revised | 2026-04-30 |

---

## 1. Executive Summary

A multi-tenant B2B commerce backbone built as a small, opinionated set of microservices. The platform serves wholesale buyers and merchant tenants who transact through partner storefronts, marketplace portals, or direct API integration. The goal is a production-grade reference architecture that any future business module (catalog extensions, billing, fulfilment, returns) can plug into without re-deciding cross-cutting concerns.

## 2. Business Goals

| # | Goal | Why it matters |
|---|---|---|
| G1 | Onboard a new tenant in minutes, not days | Tenant acquisition is the primary growth lever |
| G2 | Process orders idempotently under flaky network conditions | Duplicate orders create refund cost and trust loss |
| G3 | Support per-tenant data and behaviour isolation | Required for enterprise customers and certain regulated markets |
| G4 | Operate with full audit & traceability of money-affecting events | Required for finance reconciliation and dispute handling |
| G5 | Independent service evolution and deploy cadence | Catalog and Order teams ship at different speeds |
| G6 | Predictable cost-per-transaction at 10× current load | Revenue scales with order volume; infra cost must not |
| G7 | Enable vendor self-onboarding with platform approval workflow | Reduces ops overhead for merchant expansion |
| G8 | Drive repeat purchase through discount and coupon campaigns | Increases average order value and loyalty |

## 3. Stakeholders

| Role | Interest |
|---|---|
| Tenant Admin | Manages users, products, branding for their tenant |
| Buyer / End User | Places orders, tracks shipments, manages basket |
| Customer Success | Provisions tenants, resets passwords, audits activity |
| Finance | Reconciles orders, refunds, invoices, payment records |
| Vendor | Registers, maintains profile, fulfils shipments |
| SRE / Platform | Deploys, observes, scales, pages on incidents |
| Backend Engineer | Adds features, fixes bugs, owns service code |
| Security / Compliance | Reviews auth, secrets, PII handling, audit trails |

## 4. Scope

### 4.1 In scope (current release)

- **Identity & Access** — tenant registration, user registration, login, JWT issuance, refresh tokens, role-based authorization
- **Catalog** — product CRUD, category management, low-stock signalling, stock reservation/release
- **Order** — order creation (idempotent), state transitions (Pending → Confirmed → Processing → Shipped → Delivered / Cancelled), order history per tenant
- **Basket** — ephemeral Redis-backed shopping basket: add/update/remove items, apply coupon, checkout to order
- **Payment & Invoice** — process payments, track payment status, generate invoices, mark paid, cancel, refund
- **Shipping** — create shipments, dispatch, mark delivered, track with carrier + tracking number
- **Vendor** — vendor registration, platform approval, profile management, suspend/reactivate/deactivate lifecycle
- **Discount & Coupon** — discount rules (percentage/fixed/free-shipping), coupon codes with usage limits and expiry
- **Review** — product review submission, moderation (approve/reject), public display
- **Order Fulfillment Saga** — MassTransit state machine orchestrating stock reservation, payment, and shipment across services; compensating transactions on failure
- **Notification** — async email on order confirmed, order processing started, order fulfilled, order cancelled (stock/payment/shipment failures), user registration welcome, low-stock alert to tenant admin
- **API Gateway** — single ingress, JWT validation, route fan-out, health checks, rate limiting
- **Operations** — distributed tracing, structured logs, health checks, containerised local stack

### 4.2 Out of scope (this release)

| Item | Reason |
|---|---|
| Real payment gateway (PCI) | Stub implementation wired; real PCI-scope gateway deferred to a billing service |
| Returns & RMA | Depends on real payment gateway integration |
| Search (full-text, faceted) | Will be added once catalog volume justifies a dedicated read store |
| Front-end / storefront UI | Owned by partner integrators; this platform is API-first |
| Multi-region active-active | Single region until SLAs require otherwise |

## 5. Functional Requirements

### 5.1 Identity Service (`/api/identity/*`)

| ID | Requirement |
|---|---|
| FR-ID-1 | A tenant admin can register a new tenant with a unique slug |
| FR-ID-2 | A user can register against an existing tenant with email + password |
| FR-ID-3 | A user can authenticate and receive a short-lived JWT (60 min) plus a refresh token |
| FR-ID-4 | The JWT carries `user_id`, `tenant_id`, `tenant_slug`, and `roles` claims |
| FR-ID-5 | A user can refresh an expired JWT using a valid refresh token |
| FR-ID-6 | Passwords are stored using BCrypt; plaintext is never persisted or logged |

### 5.2 Product Service (`/api/products/*`)

| ID | Requirement |
|---|---|
| FR-PR-1 | A tenant user can create a product with SKU, name, price, currency, stock |
| FR-PR-2 | SKU is unique within a tenant |
| FR-PR-3 | A user can list products with paging and filtering, scoped to their tenant |
| FR-PR-4 | A user can fetch a single product by id |
| FR-PR-5 | When stock falls below a configured threshold, the system emits a low-stock event |
| FR-PR-6 | Read endpoints serve from cache when fresh; cache invalidates on mutation |
| FR-PR-7 | The Product service handles stock reservation and release commands from the Order fulfillment saga |

### 5.3 Order Service (`/api/orders/*`)

| ID | Requirement |
|---|---|
| FR-OR-1 | A buyer can create an order with shipping address, optional billing address, and at least one item |
| FR-OR-2 | Orders submitted with the same `Idempotency-Key` header within 24h return the original result; no duplicate order is created |
| FR-OR-3 | A buyer can list their tenant's orders with paging and status filter |
| FR-OR-4 | An order moves through a fixed lifecycle (Pending → Confirmed → Processing → Shipped → Delivered) with Cancel allowed before Delivered |
| FR-OR-5 | Order totals (subtotal, tax, shipping, total) are computed by the domain, not the client |
| FR-OR-6 | Order confirmed, shipped, and fulfilled emit integration events consumed by the Notification worker |
| FR-OR-7 | The Order fulfillment saga orchestrates stock reservation → payment → shipment, with compensating rollback on any step failure |
| FR-OR-8 | Saga timeout: if stock reservation is not acknowledged within the configured window, the order is automatically cancelled |

### 5.4 Basket Service (`/api/basket/*`)

| ID | Requirement |
|---|---|
| FR-BS-1 | A buyer can add a product to their basket; duplicate items increment quantity |
| FR-BS-2 | A buyer can update the quantity of a basket item |
| FR-BS-3 | A buyer can remove an individual item from the basket |
| FR-BS-4 | A buyer can clear their entire basket |
| FR-BS-5 | A buyer can apply a coupon code to the basket; the system validates the coupon before accepting |
| FR-BS-6 | A buyer can checkout the basket, which publishes an integration event consumed by the Order service to create an order |
| FR-BS-7 | The basket is ephemeral and backed by Redis; it is not persisted to a relational database |
| FR-BS-8 | Basket data is scoped to the authenticated user and tenant |

### 5.5 Payment Service (`/api/payments/*`, `/api/invoices/*`)

| ID | Requirement |
|---|---|
| FR-PM-1 | The system can process a payment for an order with amount, currency, and payment method |
| FR-PM-2 | A payment has a status lifecycle: Pending → Completed / Failed / Refunded |
| FR-PM-3 | The system can generate an invoice for a payment with line items and totals |
| FR-PM-4 | An invoice can be marked as Paid or Cancelled |
| FR-PM-5 | A completed payment can be fully refunded |
| FR-PM-6 | Payment and invoice records are queryable by order, by tenant, and by individual ID |

### 5.6 Shipping Service (`/api/shipments/*`)

| ID | Requirement |
|---|---|
| FR-SH-1 | The system can create a shipment for an order with carrier, recipient, address, and shipping cost |
| FR-SH-2 | A shipment ID is unique per order; duplicate shipments for the same order are rejected |
| FR-SH-3 | A shipment moves through: Pending → Shipped → Delivered (Cancel allowed before Delivered) |
| FR-SH-4 | Dispatching a shipment records a `ShippedAt` timestamp and raises a domain event |
| FR-SH-5 | Marking a shipment delivered records a `DeliveredAt` timestamp and raises a domain event |
| FR-SH-6 | Tracking numbers are auto-generated (prefix `B2B-`) and can be updated externally |

### 5.7 Vendor Service (`/api/vendors/*`)

| ID | Requirement |
|---|---|
| FR-VN-1 | A vendor can self-register with company name, email, tax ID, and address |
| FR-VN-2 | Email and Tax ID are unique within a tenant |
| FR-VN-3 | A newly registered vendor starts in `PendingApproval` status |
| FR-VN-4 | A platform admin can approve a vendor and set a commission rate (0–100 %) |
| FR-VN-5 | A platform admin can suspend an active vendor with a reason |
| FR-VN-6 | A suspended vendor can be reactivated to `Active` |
| FR-VN-7 | A vendor can be permanently deactivated |
| FR-VN-8 | A vendor can update their profile (company name, contact, address, website, description) |

### 5.8 Discount Service (`/api/discounts/*`, `/api/coupons/*`)

| ID | Requirement |
|---|---|
| FR-DS-1 | A tenant admin can create a discount rule with type (Percentage / Fixed / FreeShipping), value, and optional minimum order amount |
| FR-DS-2 | A tenant admin can deactivate a discount rule |
| FR-DS-3 | A tenant admin can create coupon codes linked to a discount rule |
| FR-DS-4 | Coupon codes have optional usage limits and expiry dates |
| FR-DS-5 | A buyer can validate a coupon code; the response includes the discount details |
| FR-DS-6 | A buyer can apply a coupon to their basket; the basket reflects the discounted total |
| FR-DS-7 | Applying a coupon decrements the usage count; expired or exhausted coupons are rejected |

### 5.9 Review Service (`/api/reviews/*`)

| ID | Requirement |
|---|---|
| FR-RV-1 | A buyer can submit a product review with rating (1–5), title, and body |
| FR-RV-2 | A submitted review enters `Pending` moderation status |
| FR-RV-3 | A moderator can approve a review, making it publicly visible |
| FR-RV-4 | A moderator can reject a review with a reason |
| FR-RV-5 | A buyer can list their own submitted reviews |
| FR-RV-6 | Anyone can list approved reviews for a product |
| FR-RV-7 | One review per buyer per product is enforced |

### 5.10 Notification Worker

| ID | Requirement |
|---|---|
| FR-NT-1 | On `OrderConfirmed`, send a confirmation email to the buyer |
| FR-NT-2 | On `OrderProcessingStarted`, send a processing notification to the buyer |
| FR-NT-3 | On `OrderFulfilled`, send a delivery confirmation email to the buyer |
| FR-NT-4 | On `OrderCancelledDueToStock`, notify the buyer of stock-driven cancellation |
| FR-NT-5 | On `OrderCancelledDueToPayment`, notify the buyer of payment failure |
| FR-NT-6 | On `OrderCancelledDueToShipment`, notify the buyer of shipment failure |
| FR-NT-7 | On `UserRegistered`, send a welcome email |
| FR-NT-8 | On `ProductLowStock`, alert the tenant admin |
| FR-NT-9 | A failed delivery is retried with exponential backoff before being parked |

### 5.11 Gateway

| ID | Requirement |
|---|---|
| FR-GW-1 | All public traffic enters through the gateway on port 5000 |
| FR-GW-2 | The gateway validates JWTs before forwarding to protected routes |
| FR-GW-3 | The gateway transparently strips its routing prefix when forwarding upstream |
| FR-GW-4 | Each upstream cluster is health-checked every 10s; unhealthy nodes are removed from rotation |
| FR-GW-5 | Per-IP rate limiting is enforced at the gateway (fixed window + sliding window policies) |

## 6. Non-Functional Requirements

| ID | Category | Requirement | Target |
|---|---|---|---|
| NFR-1 | Performance | p95 read latency at the gateway | < 250 ms |
| NFR-2 | Performance | p95 order-create latency | < 600 ms |
| NFR-3 | Throughput | Steady-state orders per second per service replica | ≥ 100 |
| NFR-4 | Availability | Per-service monthly availability | ≥ 99.5 % |
| NFR-5 | Durability | No order lost if a service crashes mid-write | Hard requirement |
| NFR-6 | Security | All inter-service ingress authenticated by JWT | Hard requirement |
| NFR-7 | Security | Secrets sourced from environment / vault, never committed | Hard requirement |
| NFR-8 | Multi-tenancy | One tenant cannot read or mutate another tenant's data | Hard requirement |
| NFR-9 | Observability | Every request gets a trace id propagated end-to-end | Hard requirement |
| NFR-10 | Observability | All services emit structured JSON logs to a central sink | Hard requirement |
| NFR-11 | Idempotency | Duplicate POST `/api/orders` with same `Idempotency-Key` returns the original 201 | 24h window |
| NFR-12 | Compliance | Auditable history of order state transitions | Retained ≥ 7 years |
| NFR-13 | Recoverability | RPO ≤ 5 min, RTO ≤ 30 min for the order datastore | Disaster scenario |

## 7. Constraints & Assumptions

- Greenfield platform; no legacy schema or data to migrate
- PostgreSQL is the system of record; Basket uses Redis as its only data store (ephemeral by design)
- A single region is acceptable until annualised order revenue justifies multi-region
- All clients consume HTTP/JSON; no gRPC or GraphQL for the initial release
- Event ordering is per-aggregate (per-order, per-product), not global
- Open-source-only stack; no proprietary cloud services in the core

## 8. Acceptance Criteria (per release)

| # | Criterion |
|---|---|
| AC-1 | All in-scope endpoints documented in OpenAPI / Scalar UI |
| AC-2 | Test suite green (unit ≥ 80 % line coverage on Domain + Application) |
| AC-3 | `docker-compose up -d` brings the full stack up on a developer laptop in ≤ 2 min |
| AC-4 | A new tenant can be onboarded and place an order via gateway in < 10 minutes from a fresh clone |
| AC-5 | Idempotent order creation verified by replaying the same `Idempotency-Key` and asserting one DB row |
| AC-6 | Trace appears in Jaeger spanning Gateway → Order Service → RabbitMQ → Notification Worker |
| AC-7 | Order fulfillment saga happy path verified end-to-end: stock reserved → payment processed → shipment created → order fulfilled |
| AC-8 | Saga compensation verified: stock failure cancels the order and releases any reserved stock |
| AC-9 | Basket checkout publishes an integration event that creates an order via the Order service |
| AC-10 | Coupon validation rejects expired, exhausted, and non-existent codes |
| AC-11 | Vendor registration rejected when email or Tax ID is already registered in the same tenant |

## 9. Risks & Mitigations

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Cross-tenant data leak | Critical | Low | Mandatory `TenantId` filter on every query; enforced at code review |
| Lost integration event after crash | High | Medium | In-memory outbox (current); persistent EF outbox on roadmap (P0) |
| Hot-tenant noisy-neighbour | High | Medium | Per-tenant rate limit at gateway (roadmap) + per-tenant pool caps (roadmap) |
| Cache stampede on hot product | Medium | Medium | Single-flight lock or probabilistic refresh (roadmap) |
| Refresh-token theft | High | Low | Short JWT TTL + rotation on refresh; max 5 concurrent active tokens per user |
| RabbitMQ outage | High | Low | Producers buffer in outbox; consumers replay on recovery |
| Saga timeout without cleanup | Medium | Low | Saga timeout scheduling via RabbitMQ delayed message exchange; compensating Cancel raised on timeout |
| Redis outage for basket | Medium | Low | Basket unavailable during outage; recovers on Redis restart; basket is intentionally ephemeral |
| Coupon over-redemption under concurrency | Medium | Low | Atomic Redis decrement on apply; optimistic concurrency on DB row for usage count |

## 10. Glossary

| Term | Meaning |
|---|---|
| Tenant | A merchant organisation isolated from other merchants |
| Aggregate | A consistency boundary in DDD — Order, Product, User, Vendor, etc. |
| Domain event | In-process notification raised by an aggregate |
| Integration event | Cross-service async message published over RabbitMQ |
| Idempotent | Repeated execution yields the same result, side-effects happen at most once |
| CQRS | Command-Query Responsibility Segregation |
| YARP | Yet Another Reverse Proxy (the Microsoft library) |
| Saga | Long-running stateful workflow coordinating multiple services with compensating transactions |
| Compensating transaction | An action that undoes a previously completed step when a later step fails |
| Basket | Ephemeral Redis-backed collection of items a buyer intends to purchase |
| Coupon | A redeemable code linked to a discount rule, optionally limited by usage count or expiry |
| Commission rate | Percentage fee charged to a vendor on orders fulfilled through the platform |
