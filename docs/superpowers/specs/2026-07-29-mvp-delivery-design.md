<!-- markdownlint-disable MD013 -->

# MVP Delivery Design

## Status

Approved for autonomous implementation on 2026-07-29 from the product-owner direction, accepted ADRs and reconciled evidence register. Production activation remains separately gated.

## Objective

Deliver the `readytogo.travel` consumer MVP as incremental, independently testable vertical slices while keeping LiteAPI, payment and legal production capabilities disabled until their evidence gates are approved.

The MVP includes:

- hotel-only booking;
- flight-only booking;
- a combined customer journey containing linked hotel and flight bookings without pretending the suppliers perform one atomic package transaction;
- Australia-first pricing, locale and customer protections;
- customer accounts and guest-compatible support tickets;
- payment through an official LiteAPI hosted/SDK component;
- immutable booking history, webhooks and scheduled reconciliation; and
- first-party ticket support and email notifications.

## Delivery Approaches Considered

### Approach A — Vertical slices on a feature-oriented modular monolith

Build a small foundation, then land accounts/trips, search, booking/payment, reconciliation and support as complete slices with API, persistence, UI and tests.

Benefits: early working software, module ownership remains visible, supplier risk stays behind adapters and production gates can be enforced by configuration.

Trade-off: shared foundations must be deliberately small so they do not become an internal framework.

### Approach B — Complete technical platform before product behavior

Build every persistence, messaging, storage, deployment and observability abstraction before exposing a customer workflow.

Benefits: consistent infrastructure from the start.

Trade-off: long delay before product validation and high risk of speculative abstractions.

### Approach C — Supplier-first booking spike

Integrate LiteAPI search/payment/booking immediately and shape the platform around observed payloads.

Benefits: fast supplier learning.

Trade-off: makes the platform contract supplier-shaped, couples progress to production evidence and underinvests in failure recovery.

## Selected Approach

Choose Approach A.

Supplier work begins with a sandbox adapter and recorded fixtures, but public contracts and durable state remain platform-owned. Production capabilities default off. No external supplier response is allowed to bypass validation, canonicalisation, ownership, idempotency or confirmation-state rules.

## Slice Sequence

### Slice 1 — Application foundation

- .NET 10 solution and build conventions.
- API, Blazor SSR, general worker and flight-reconciliation worker deployables.
- feature-module registration boundary.
- `/api/v1` routing, OpenAPI, `ProblemDetails`, correlation ID and health endpoints.
- worker liveness and clean cancellation.
- architecture and contract tests.
- container build definitions and CI build/test workflow.

This slice has no production supplier, database or secret dependency and is the first implementation plan executed from this design.

### Slice 2 — Identity, locale, customers, trips and travellers

- Keycloak OIDC boundary and subject-linked customer.
- anonymous locale cookie plus authenticated preference.
- `en-AU` and AUD defaults with translation resource fallback.
- customer-owned trips and traveller profiles.
- booking-time traveller snapshots.
- sensitive reusable traveller fields disabled until explicit granular consent.
- adult purchasing-account rule and guardian authority fields for minor travellers.

### Slice 3 — Search and capability registry

- supplier-neutral hotel and flight search requests/results.
- LiteAPI sandbox adapter and sanitized contract fixtures.
- environment, point-of-sale, carrier and operation capability registry.
- minimum-total-price model, currency provenance, tax/fee categories and offer expiry.
- capability-gated Qantas, Jetstar and Virgin Australia observations without production claims.

### Slice 4 — Checkout, hosted payment and booking

- server-side checkout session and idempotency.
- offer verification/repricing with renewed customer acceptance.
- official LiteAPI hosted/SDK wrapper through Blazor JavaScript interop.
- separate payment and booking states.
- confirmation only after supplier confirmation.
- hotel, flight and combined journey orchestration.
- pending/unknown recovery, duplicate-return handling and support escalation.

Combined journeys are a platform trip containing separately evidenced component bookings. Unless executed terms later approve a principal/package model, payment, confirmation, cancellation and refund state remain explicit per component.

### Slice 5 — Webhooks, reconciliation, immutable versions and notifications

- authenticated at-least-once webhook ingress.
- durable inbox with `LiteAPI + environment + event_id` uniqueness.
- retrieval-driven reconciliation and immutable canonical versions.
- daily active-flight checks and hourly checks in the last 24 hours.
- notification outbox with materiality, quiet hours, retry and deduplication.
- unsupported or failed servicing converted to internal cases.

### Slice 6 — First-party support tickets and documents

- authenticated and guest ticket creation.
- UUIDv7 internal ID plus separate hashed bearer secret.
- immutable thread and exact state transitions.
- private, quarantined S3-compatible attachments.
- fail-closed malware scan and five-minute authorised downloads.
- durable email notification after message persistence.
- protected support UI with MFA/permissions and no impersonation.

### Slice 7 — Retention, privacy operations and production-readiness evidence

- record-class expiry and trigger calculations.
- legal hold, release and deletion receipts.
- privacy access/correction/deletion workflows.
- backup tombstone/recovery procedures.
- production supplier, carrier, webhook, payment and legal checklists.
- controlled sandbox/production certification evidence.

## Module Boundaries

The modular monolith contains these product modules:

- Customers
- Travellers
- Trips
- Search
- Accommodation
- Flights
- Pricing
- Bookings
- Payments
- Reconciliation
- Notifications
- Documents
- Support
- SupplierIntegrations

Each module exposes registration plus explicit application interfaces/contracts. A module owns its entity mappings and tables. Cross-module behavior uses public interfaces or durable messages; it does not query another module's private tables.

Shared building blocks are limited to identifiers, clocks, results/errors, correlation, idempotency primitives, durable-work abstractions and test utilities. Product rules do not move into a generic shared project.

## Public API and Web

- Routes begin with `/api/v1`.
- Blazor SSR calls the same public application API used by later mobile clients.
- Static SSR is the default; interactive server rendering is applied only to flows needing client interaction.
- Errors use `ProblemDetails` plus stable `code`, `correlationId`, optional `errors` and retry metadata.
- Authentication is Keycloak-issued OIDC/OAuth tokens; email is never the resource-ownership key.
- Public commands use idempotency where replay could duplicate payment, booking, cancellation, refund, document or notification effects.
- OpenAPI is generated during development and CI but not exposed publicly by default.

## Persistence and Durable Work

PostgreSQL stores authoritative state, current booking projections, immutable versions, inbox, outbox, schedules, idempotency records and operational cases.

Each durable worker cycle:

1. claims due rows with a bounded lease;
2. commits the claim before external work;
3. performs one idempotent operation;
4. records outcome, retry category and next attempt;
5. creates domain/outbox effects transactionally where applicable; and
6. releases or expires the lease safely on shutdown/failure.

Webhooks acknowledge only after the raw envelope is durably accepted or recognized as a duplicate. Supplier retrieval, domain mutation and notifications occur asynchronously.

## Security and Compliance Boundaries

- Supplier and payment credentials remain server-side or are provider-scoped browser secrets explicitly intended for the payment component.
- The platform never receives PAN, CVC/CVV, track data, PIN or raw wallet credentials.
- Full passport/identity-document values do not enter logs, canonical history or general support records.
- Ticket attachments are private and quarantined until validation and scanning complete.
- Production integrations and customer promises are capability-gated.
- Minimum total price, charging currency, supplier confirmation and component refund states are explicit.
- Travel insurance, wallet/stored value, remittance, platform FX, credit and buy-now-pay-later are absent from MVP code.
- Purchasing accounts require age 18 or older; unaccompanied-minor inventory is disabled.

## Failure Handling

The application distinguishes known failure from unknown external outcome.

- Supplier timeout after a mutating request creates an ambiguous operation and reconciliation work; it never invites immediate blind retry.
- Payment success without confirmed booking creates a recovery state and blocks duplicate payment.
- Webhook parsing failure is quarantined after bounded retries; accepted events remain inspectable.
- Unknown supplier state fails closed and raises operational visibility.
- Attachment scan failure leaves the object quarantined.
- Notification failure does not roll back the committed booking/ticket change; the outbox retries and exposes failure.
- Worker termination respects cancellation and leaves leases recoverable.

## Testing Strategy

- Unit tests for domain transitions, canonicalisation, expiry calculations, price totals and identifier/token behavior.
- Contract tests for API errors, versioned routes and supplier adapters.
- Architecture tests for project/module dependency rules.
- Integration tests against PostgreSQL, Keycloak and MinIO/ClamAV-compatible local services where relevant.
- Sanitized LiteAPI fixtures for deterministic adapter tests; sandbox tests are separate and opt-in.
- End-to-end browser tests for locale, hosted payment return/recovery, booking confirmation and guest ticket flows.
- Production certification tests remain disabled until explicit account/environment authorization.

## Environment and Activation Model

Local, test, sandbox, staging and production configuration are separate. Supplier/payment capability is identified by provider, environment, market and operation. Production defaults to disabled and cannot be enabled merely by adding a secret; the matching approval record and capability configuration are required.

No test or sandbox result is represented as production evidence.

## Success Criteria

- Each slice leaves a buildable, testable application and a reviewable commit.
- Hotel, flight and combined journeys share platform contracts without one supplier dictating the domain.
- Payment and booking are separately recoverable and cannot be duplicated by browser replay.
- Booking changes are explainable through immutable versions.
- Guests and account holders can use first-party support safely.
- Locale and Australian defaults exist from the first customer-facing slice.
- Production remains technically disabled until every relevant gate is approved.

## Self-review Record

- Placeholder scan: no implementation placeholder or silent product choice remains.
- Consistency: solution/runtime/API decisions match ADR-0006, ADR-0008, ADR-0009 and ADR-0010.
- Scope: delivery is decomposed into seven independently testable slices; only Slice 1 is executed by the first implementation plan.
- Ambiguity: combined journeys, confirmation, payment mismatch, production capability gates and mobile deferral are explicit.
