<!-- markdownlint-disable MD013 -->

# Slice 5 Booking Reconciliation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver authenticated webhook ingress, durable hotel/flight reconciliation, immutable canonical booking versions and deduplicated customer-notification intents without enabling unapproved production integrations.

**Architecture:** Extend the existing Booking feature project with focused Webhooks, Reconciliation and Notifications boundaries so current projection, version and notification intent update atomically. Keep API and worker projects as composition roots; the general worker handles inbox/hotel/notification cycles and the dedicated flight worker handles flight schedules.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10, PostgreSQL, SQLite integration tests, xUnit, existing cooperative workers and container build workflow.

## Global Constraints

- Provider payloads never become the public contract or directly mutate booking state.
- Production webhook, supplier booking/payment and notification capabilities default off.
- `LiteAPI + environment + event_id` is the webhook identity.
- Current state, immutable version and notification intent commit transactionally.
- Hotel, flight and combined-journey component outcomes remain explicit.
- Reconciliation never repeats booking, payment, settlement, refund or cancellation commands.
- Slice 6 support and Slice 7 retention/production certification are excluded.

---

### Task 1: Canonical booking model and append-only versions

**Files:**
- Create: `src/ReadyToGoTravel.Booking/Reconciliation/CanonicalBookingState.cs`
- Create: `src/ReadyToGoTravel.Booking/Reconciliation/BookingVersion.cs`
- Create: `src/ReadyToGoTravel.Booking/Reconciliation/CanonicalBookingVersioner.cs`
- Modify: `src/ReadyToGoTravel.Booking/Bookings/ComponentBooking.cs`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingDbContext.cs`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingEntityConfigurations.cs`
- Test: `tests/ReadyToGoTravel.Booking.Tests/CanonicalBookingVersionTests.cs`

**Interfaces:**
- Produces: `RetrievedBookingState`, `BookingFlightSegment`, `CanonicalBookingVersioner.Create(ComponentBooking, RetrievedBookingState, BookingVersion?, DateTimeOffset, string, string)` and append-only `BookingVersion`.
- Persists: provider-neutral canonical JSON/hash, version number, metadata flags, severity and stable diff JSON.

- [x] **Step 1: Write failing canonicalisation and immutability tests**

Add tests proving reordered segments hash identically, a meaningful field change produces a different hash and diff, unchanged state creates no second version, current projection changes with a new version, and EF rejects `Modified` or `Deleted` `BookingVersion` entries.

- [x] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj -c Release --filter FullyQualifiedName~CanonicalBookingVersionTests`

Expected: compilation/test failure because the canonical and version types do not exist.

- [x] **Step 3: Implement deterministic canonicalisation and append-only domain records**

Use explicit JSON DTOs, UTC timestamps, sorted segment identity and SHA-256. Exclude retrieval timestamp from the canonical hash. Add current canonical hash/version/last-reconciled/next-departure fields to `ComponentBooking`, and reject version mutation from `BookingDbContext.SaveChanges`/`SaveChangesAsync`.

- [x] **Step 4: Run focused tests and verify GREEN**

Run the Task 1 focused command and confirm all canonical/version tests pass.

### Task 2: Durable reconciliation schedules and provider retrieval

**Files:**
- Create: `src/ReadyToGoTravel.Booking/Reconciliation/ReconciliationWork.cs`
- Create: `src/ReadyToGoTravel.Booking/Reconciliation/ReconciliationScheduler.cs`
- Create: `src/ReadyToGoTravel.Booking/Reconciliation/BookingReconciliationProcessor.cs`
- Create: `src/ReadyToGoTravel.Booking/Reconciliation/OperationalCase.cs`
- Modify: `src/ReadyToGoTravel.Booking/Providers/BookingProviders.cs`
- Modify: `src/ReadyToGoTravel.Booking/Application/CheckoutService.cs`
- Modify: `src/ReadyToGoTravel.Booking/SupplierIntegrations/LiteApi/LiteApiFixtureBookingProvider.cs`
- Modify: `src/ReadyToGoTravel.Booking/SupplierIntegrations/LiteApi/Fixtures/booking-scenarios.json`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingDbContext.cs`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingEntityConfigurations.cs`
- Test: `tests/ReadyToGoTravel.Booking.Tests/ReconciliationTests.cs`

**Interfaces:**
- `IBookingProvider.RetrieveAsync` returns a `BookingProviderExecutionResult` with optional provider-neutral `RetrievedBookingState`.
- `IReconciliationWorkProcessor.ProcessNextAsync(CheckoutProduct? product, string workerId, CancellationToken)` claims and processes at most one due row.
- `IReconciliationScheduler.EnqueueImmediateAsync(Guid componentBookingId, string source, string correlationId, CancellationToken)` advances an existing schedule or inserts one.

- [x] **Step 1: Write failing schedule, lease and convergence tests**

Cover initial scheduling after a component receives a provider reference, missed-webhook scheduled retrieval, duplicate enqueue collapsing to one row, expired lease reclaim, active hotel daily cadence, flight daily/hourly cadence, terminal stop, retrieval failure retry, reference/product mismatch case creation and cancellation-token propagation.

- [x] **Step 2: Run focused reconciliation tests and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj -c Release --filter FullyQualifiedName~ReconciliationTests`

Expected: compilation/test failure because scheduler and processor contracts do not exist.

- [x] **Step 3: Implement atomic claims, retrieval and next-due calculation**

Use a single schedule row per component, an atomic conditional `ExecuteUpdateAsync` claim, a bounded lease, sanitised failures and exponential retry. Invoke only provider retrieval. On success, reload the component, apply the canonical version transactionally and calculate the next due time from product, lifecycle and earliest future flight departure.

- [x] **Step 4: Integrate checkout completion with initial work creation**

After each persisted booking outcome with a provider reference, enqueue its reconciliation row using the same `BookingDbContext`; do not call retrieval or notification inline in the customer request.

- [x] **Step 5: Run focused tests and verify GREEN**

Run the Task 2 focused command and confirm all reconciliation tests pass.

### Task 3: Authenticated durable webhook inbox

**Files:**
- Create: `src/ReadyToGoTravel.Booking/Webhooks/LiteApiWebhookOptions.cs`
- Create: `src/ReadyToGoTravel.Booking/Webhooks/WebhookInboxItem.cs`
- Create: `src/ReadyToGoTravel.Booking/Webhooks/WebhookInboxService.cs`
- Create: `src/ReadyToGoTravel.Booking/Webhooks/WebhookInboxProcessor.cs`
- Create: `src/ReadyToGoTravel.Booking/Http/WebhookEndpoints.cs`
- Modify: `src/ReadyToGoTravel.Booking/BookingModule.cs`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingDbContext.cs`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingEntityConfigurations.cs`
- Modify: `src/ReadyToGoTravel.Api/Program.cs`
- Modify: `src/ReadyToGoTravel.Api/appsettings.json`
- Modify: `src/ReadyToGoTravel.Api/appsettings.Development.json`
- Test: `tests/ReadyToGoTravel.Booking.Tests/WebhookIngressTests.cs`
- Test: `tests/ReadyToGoTravel.Booking.Tests/WebhookInboxProcessorTests.cs`

**Interfaces:**
- `IWebhookInboxWriter.AcceptAsync(WebhookEnvelopeInput input, CancellationToken)` returns accepted, duplicate or conflict after persistence.
- `IWebhookInboxProcessor.ProcessNextAsync(string workerId, CancellationToken)` claims at most one inbox record and enqueues reconciliation through `IReconciliationScheduler`.

- [x] **Step 1: Write failing webhook authentication and acknowledgement tests**

Cover disabled endpoint, non-JSON, oversized body, missing/invalid/current/previous secret, environment mismatch, malformed top-level envelope, durable accepted `202`, identical duplicate `202`, same identity/different hash conflict, no token persistence and no consumer JWT requirement.

- [x] **Step 2: Run ingress tests and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj -c Release --filter FullyQualifiedName~WebhookIngressTests`

Expected: `404`/compilation failure because no webhook endpoint exists.

- [x] **Step 3: Implement the dedicated endpoint and inbox writer**

Read the request through a bounded stream, compare UTF-8 secret bytes with `CryptographicOperations.FixedTimeEquals`, parse only the envelope, validate route/payload environment and commit the exact body/hash before returning `202 Accepted`.

- [x] **Step 4: Write RED inbox-processing tests**

Cover known hotel/flight events, stringified nested request/response reference extraction, unsupported event case, malformed nested payload quarantine, missing booking reference case, duplicate internal replay and transient retry on database/provider scheduling failure.

- [x] **Step 5: Implement asynchronous inbox processing and verify GREEN**

Process allow-listed lifecycle events as immediate retrieval triggers only. Mark unsupported or poison events inspectably without changing current booking state from the webhook body. Run both Task 3 focused test filters.

### Task 4: Notification outbox, materiality and quiet hours

**Files:**
- Create: `src/ReadyToGoTravel.Booking/Notifications/BookingChangeClassifier.cs`
- Create: `src/ReadyToGoTravel.Booking/Notifications/NotificationOutboxItem.cs`
- Create: `src/ReadyToGoTravel.Booking/Notifications/NotificationOutboxProcessor.cs`
- Modify: `src/ReadyToGoTravel.Booking/Reconciliation/BookingReconciliationProcessor.cs`
- Modify: `src/ReadyToGoTravel.Booking/Checkout/CheckoutSession.cs`
- Modify: `src/ReadyToGoTravel.Consumer/Application/ConsumerBookingContext.cs`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingDbContext.cs`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingEntityConfigurations.cs`
- Test: `tests/ReadyToGoTravel.Booking.Tests/NotificationTests.cs`
- Test: `tests/ReadyToGoTravel.Consumer.Tests/ConsumerBookingContextTests.cs`

**Interfaces:**
- `ICustomerNotificationSender.SendAsync(CustomerNotification message, CancellationToken)` is the provider-neutral outbound boundary.
- `INotificationOutboxProcessor.ProcessNextAsync(string workerId, CancellationToken)` leases and delivers one due item.
- Checkout snapshots `PreferredLocale` and the approved `Australia/Sydney` notification timezone through the Consumer application contract.

- [x] **Step 1: Write failing classifier, quiet-hour and outbox tests**

Cover initial-version no notification; informational, under-30-minute, 30-minute, airport/flight-number, hotel inclusion/policy, cancellation and relocation classification; quiet-hour deferral; material bypass; unique notification dedupe; transient retry; permanent failure visibility; and cancellation propagation.

- [x] **Step 2: Run notification tests and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj -c Release --filter FullyQualifiedName~NotificationTests`

Expected: compilation/test failure because classifier/outbox contracts do not exist.

- [x] **Step 3: Implement transactional notification intent and worker delivery**

Create outbox rows in the same save as the new version/current projection. Use version/component/customer/template/channel as the unique delivery key. Calculate `not_before` in `Australia/Sydney`; record attempts and bounded retry without changing the booking transaction.

- [x] **Step 4: Verify customer locale snapshot and GREEN tests**

Run the Booking notification filter and `dotnet test tests/ReadyToGoTravel.Consumer.Tests/ReadyToGoTravel.Consumer.Tests.csproj -c Release --filter FullyQualifiedName~ConsumerBookingContextTests`.

### Task 5: Customer-safe history API and private worker composition

**Files:**
- Create: `src/ReadyToGoTravel.Booking/Http/BookingHistoryContracts.cs`
- Create: `src/ReadyToGoTravel.Booking/Application/BookingHistoryService.cs`
- Modify: `src/ReadyToGoTravel.Booking/Http/BookingEndpoints.cs`
- Modify: `src/ReadyToGoTravel.Booking/BookingModule.cs`
- Modify: `src/ReadyToGoTravel.Worker/Program.cs`
- Modify: `src/ReadyToGoTravel.Worker/Worker.cs`
- Modify: `src/ReadyToGoTravel.Worker/ReadyToGoTravel.Worker.csproj`
- Modify: `src/ReadyToGoTravel.FlightReconciliation.Worker/Program.cs`
- Modify: `src/ReadyToGoTravel.FlightReconciliation.Worker/Worker.cs`
- Modify: `src/ReadyToGoTravel.FlightReconciliation.Worker/ReadyToGoTravel.FlightReconciliation.Worker.csproj`
- Modify: worker appsettings files
- Test: `tests/ReadyToGoTravel.Booking.Tests/BookingHistoryApiTests.cs`
- Test: `tests/ReadyToGoTravel.Architecture.Tests/SolutionConventionsTests.cs`
- Test: `tests/ReadyToGoTravel.BuildingBlocks.Tests/CooperativeWorkerTests.cs`

**Interfaces:**
- `GET /api/v1/checkouts/{checkoutId}/history` returns component product/status, ordered version metadata, flags and stable diff only after `sub` ownership validation.
- General worker executes inbox, hotel reconciliation and notification cycles; flight worker executes flight reconciliation only.

- [x] **Step 1: Write failing owner-isolation and data-leakage tests**

Cover owner success, another customer `404`, anonymous `401`, combined-component separation, ordering, and absence of provider binding/reference, raw body, secret, internal error and notification destination fields.

- [x] **Step 2: Run history tests and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj -c Release --filter FullyQualifiedName~BookingHistoryApiTests`

Expected: `404` because the history route does not exist.

- [x] **Step 3: Implement history query and worker composition**

Query Booking-owned tables only after resolving the caller's customer ID. Register the shared Booking context in both private workers with their existing connection string, and keep their `ExecuteCycleAsync` methods cancellation-aware and bounded to one item per processor per cycle.

- [x] **Step 4: Run focused worker/history tests and verify GREEN**

Run the history, architecture and building-block test projects.

### Task 6: Persistence migration and PostgreSQL guarantees

**Files:**
- Create: `src/ReadyToGoTravel.Booking/Persistence/Migrations/20260808010000_Slice5BookingReconciliation.cs`
- Create: `src/ReadyToGoTravel.Booking/Persistence/Migrations/20260808010000_Slice5BookingReconciliation.Designer.cs`
- Modify: `src/ReadyToGoTravel.Booking/Persistence/Migrations/BookingDbContextModelSnapshot.cs`
- Modify: `tests/ReadyToGoTravel.Booking.Tests/BookingPersistenceTests.cs`

**Interfaces:**
- Adds booking versions, reconciliation work/attempts, webhook inbox, notification outbox and operational cases with all dedupe/claim indexes.
- Installs PostgreSQL trigger `booking.reject_booking_version_mutation()` for `UPDATE`/`DELETE`.

- [x] **Step 1: Write failing persistence/model tests**

Assert all unique indexes, concurrency tokens, maximum lengths, raw-body/provider fields not exposed from public contracts, version immutability guard and deterministic migration/model parity.

- [x] **Step 2: Generate and normalise the migration**

Run: `dotnet ef migrations add Slice5BookingReconciliation --project src/ReadyToGoTravel.Booking --startup-project src/ReadyToGoTravel.Api --context BookingDbContext --output-dir Persistence/Migrations`

Rename the generated migration deterministically to `20260808010000`, add the PostgreSQL immutability function/trigger SQL, and keep the designer/snapshot identifiers aligned.

- [x] **Step 3: Run persistence and EF parity checks**

Run the Booking persistence tests, then `dotnet ef migrations has-pending-model-changes --project src/ReadyToGoTravel.Booking --startup-project src/ReadyToGoTravel.Api --context BookingDbContext` and expect no pending changes.

### Task 7: Documentation, regression verification and logical commits

**Files:**
- Modify: `README.md`
- Modify: `docs/README.md`
- Modify: `docs/api/README.md`
- Modify: `docs/domain/booking-reconciliation-and-version-history.md`
- Modify: `docs/decisions/review-register.md`
- Modify: `docs/plans/active/PLAN-0002-mvp-delivery.md`
- Modify: `docs/delivery/README.md`
- Create: `docs/delivery/2026-08-08-slice-5-booking-reconciliation-outcome.md`
- Modify: `docs/deployment/local-development.md`

- [x] **Step 1: Update docs with implemented behavior and unchanged gates**

Record endpoint authentication/acknowledgement, worker ownership, canonical/version schema, schedule rules, notification behavior, local configuration and verification. Mark Slice 5 implemented, but keep PLAN-0002 active for Slices 6–7 and OI-0002/OI-0003/OI-0005/OI-0006 production gates open.

- [x] **Step 2: Run focused and full verification**

Run:

```bash
dotnet restore ReadyToGoTravel.slnx --locked-mode
dotnet format ReadyToGoTravel.slnx --no-restore --verify-no-changes
dotnet build ReadyToGoTravel.slnx -c Release --no-restore
dotnet test ReadyToGoTravel.slnx -c Release --no-build --no-restore
dotnet ef migrations has-pending-model-changes --project src/ReadyToGoTravel.Booking --startup-project src/ReadyToGoTravel.Api --context BookingDbContext
bash scripts/validate-docs.sh
git diff --check
docker build -f src/ReadyToGoTravel.Api/Dockerfile .
docker build -f src/ReadyToGoTravel.Web/Dockerfile .
docker build -f src/ReadyToGoTravel.Worker/Dockerfile .
docker build -f src/ReadyToGoTravel.FlightReconciliation.Worker/Dockerfile .
```

- [ ] **Step 3: Review the complete diff against Slice 5 risks**

Inspect concurrency, duplicate side effects, webhook trust, stale/out-of-order retrieval, immutable history, customer isolation, provider leakage, cancellation, logging, dead code and docs. Reproduce every valid concern with a failing test before fixing it.

- [ ] **Step 4: Create logical commits and publish the PR**

Commit design/plan, core persistence/reconciliation, ingress/workers, notifications/history and docs/verification as meaningful units where the final diff supports them. Push `codex/slice-5-booking-reconciliation`, open a non-draft PR to `dev`, and include exact checks and unchanged production gates.

- [ ] **Step 5: Run independent Codex and Claude reviews**

Use a fresh reviewer context for the full PR and invoke Claude Code separately when available. Post actionable findings, fix genuine in-scope issues with RED/GREEN tests, push follow-up commits, respond to discussions and rerun affected plus full verification. Do not merge.

## Plan self-review

- Spec coverage: ingress, inbox, retrieval, schedule, versions, notifications, workers, API, migration, documentation and gates each have an implementation/test task.
- Placeholder scan: no TBD/TODO or undefined follow-up remains.
- Type consistency: worker, scheduler, provider retrieval, history and notification interfaces are introduced before their consumers.
