<!-- markdownlint-disable MD013 -->

# Slice 7 Retention, Legal Hold, Privacy Operations and Production-Readiness Certification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved retention/expiry schedule, a legal-hold model that suppresses ordinary deletion within its scope, privacy minimisation, customer-initiated account closure, and a production-readiness certification catalog, without inventing a new retention policy, without bypassing the append-only `booking_versions` immutability trigger, and without enabling any production capability by default.

**Architecture:** Add a new `ReadyToGoTravel.Retention` feature module (own `retention` PostgreSQL schema, own `RetentionDbContext`) owning legal holds, the retention policy catalog and the production-readiness catalog, following the exact module/DbContext/migration/Http shape of `ReadyToGoTravel.Support`. Booking, Support and Consumer each implement their own sweep against their own schema, calling Retention's public contracts (`ILegalHoldGuard`, `IRetentionReceiptRecorder`) — never the reverse, and never each other's private tables. Wire sweeps into the existing `ReadyToGoTravel.Worker` host; no new deployable container.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10, PostgreSQL, SQLite integration tests, xUnit, the existing `CooperativeWorker`/claim-lease idioms where applicable, and a new stateless live-recomputation sweep idiom for retention (see design doc rationale).

**Design reference:** [Slice 7 Design](../specs/2026-08-10-slice-7-retention-privacy-production-readiness-design.md).

## Global Constraints

- Ambiguous classification fails closed: if a record's trigger date cannot be precisely and safely derived from an existing field (canonical booking evidence, raw supplier payloads, traveller DOB/passport), implement the policy/calculator only and do not build a live sweep against a guessed proxy field. Document the limitation; do not claim it is enforced.
- Legal hold always overrides ordinary deletion within its explicit scope; an unrelated hold must never suppress deletion. No `if any legal hold exists, stop everything` shortcut anywhere.
- Every sweep mutation is an idempotent, individually safe operation (conditional bulk `ExecuteUpdateAsync`, or a per-item existence/hold re-check immediately before a hard delete). No new durable lease/queue table for retention.
- No deletion crosses customer/ticket/booking boundaries. No broad, unscoped `DELETE`/`ExecuteDeleteAsync` without a WHERE clause tied to the specific expired record's own key.
- `Retention:Enabled` defaults `false` in both checked-in Production and Development `appsettings.json` (opt-in only, matching the Slice 6 `Support:Storage`/`Support:Scanning` precedent). Legal-hold administration is not gated by this flag.
- `Directory.Build.props` `TreatWarningsAsErrors` stays `true`; the new project targets `net10.0` and is named `ReadyToGoTravel.Retention`.
- Slice 7 does not bypass the `booking.reject_booking_version_mutation()` trigger, does not fabricate LiteAPI/legal/PCI/carrier evidence, and does not enable any disabled production capability.

---

### Task 1: Retention module skeleton, policy catalog, legal-hold domain and persistence

**Files:**
- Create: `src/ReadyToGoTravel.Retention/ReadyToGoTravel.Retention.csproj`
- Create: `src/ReadyToGoTravel.Retention/RetentionModule.cs`
- Create: `src/ReadyToGoTravel.Retention/Domain/RetentionRecordClass.cs`
- Create: `src/ReadyToGoTravel.Retention/Domain/RetentionAction.cs`
- Create: `src/ReadyToGoTravel.Retention/Domain/RetentionPolicyCatalog.cs`
- Create: `src/ReadyToGoTravel.Retention/Domain/LegalHold.cs`
- Create: `src/ReadyToGoTravel.Retention/Domain/LegalHoldScope.cs`
- Create: `src/ReadyToGoTravel.Retention/Domain/LegalHoldAuditEvent.cs`
- Create: `src/ReadyToGoTravel.Retention/Domain/RetentionDeletionReceipt.cs`
- Create: `src/ReadyToGoTravel.Retention/Domain/RetentionOperationalCase.cs`
- Create: `src/ReadyToGoTravel.Retention/Application/LegalHoldService.cs`
- Create: `src/ReadyToGoTravel.Retention/Application/ILegalHoldGuard.cs`
- Create: `src/ReadyToGoTravel.Retention/Application/IRetentionReceiptRecorder.cs`
- Create: `src/ReadyToGoTravel.Retention/Persistence/RetentionDbContext.cs`
- Create: `src/ReadyToGoTravel.Retention/Persistence/RetentionEntityConfigurations.cs`
- Create: `src/ReadyToGoTravel.Retention/Persistence/RetentionDesignTimeDbContextFactory.cs`
- Create: `src/ReadyToGoTravel.Retention/Persistence/Migrations/20260810060000_InitialRetentionSchema.cs` (+ generated `.Designer.cs`/snapshot)
- Modify: `ReadyToGoTravel.slnx`
- Test: `tests/ReadyToGoTravel.Retention.Tests/ReadyToGoTravel.Retention.Tests.csproj`
- Test: `tests/ReadyToGoTravel.Retention.Tests/RetentionPolicyCatalogTests.cs`
- Test: `tests/ReadyToGoTravel.Retention.Tests/LegalHoldServiceTests.cs`
- Test: `tests/ReadyToGoTravel.Retention.Tests/RetentionPersistenceTests.cs`

**Interfaces:**
- Produces: `enum RetentionRecordClass { BookingRelatedSupportTicket, GeneralSupportTicket, SupportAttachment, SecurityAuditRecord, WebhookPayloadBody, NotificationRenderedContent, AbandonedCheckoutState, CanonicalBookingEvidence, SuccessfulSupplierPayload, ExceptionalSupplierPayload, TravellerSensitiveFieldMinimisation }`.
- Produces: `enum RetentionAction { Delete, DeIdentify, PolicyOnlyNoLiveSweep }`.
- Produces: `static class RetentionPolicyCatalog { static RetentionPolicyDefinition Get(RetentionRecordClass); static DateTimeOffset CalculateExpiry(RetentionRecordClass, DateTimeOffset triggerAtUtc); static bool IsExpired(RetentionRecordClass, DateTimeOffset triggerAtUtc, DateTimeOffset nowUtc); }` where `RetentionPolicyDefinition(RetentionRecordClass RecordClass, int PolicyVersion, TimeSpan RetentionPeriod, string TriggerDescription, RetentionAction Action)` is a `public sealed record`. Encodes every row of the approved schedule (data-retention-and-legal-hold.md + OI-0011), including the policy-only classes.
- Produces: `LegalHold.Open(matterReference, reason, authorizedOwnerSubject, reviewByUtc, IReadOnlyCollection<(RetentionRecordClass, Guid? customerId, Guid? componentBookingId, Guid? supportTicketId)> scopes, TimeProvider) : LegalHold`; `LegalHold.Release(releasedBySubject, releaseReason, TimeProvider)`; `LegalHold.IsActive`.
- Produces: `ILegalHoldGuard { Task<IReadOnlySet<Guid>> ExcludeHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, IReadOnlyCollection<Guid> candidateSubjectIds, CancellationToken ct); Task<bool> IsHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, Guid subjectId, CancellationToken ct); }` where `enum RetentionSubjectKind { Customer, ComponentBooking, SupportTicket }`. `IsHeldAsync` is the precise, immediately-before-action recheck; `ExcludeHeldAsync` is the coarse batch-level pre-filter.
- Produces: `IRetentionReceiptRecorder { Task RecordAsync(RetentionRecordClass recordClass, int policyVersion, string action, int successCount, int failureCount, DateTimeOffset completedAtUtc, string? failureSummary, CancellationToken ct); Task RecordOperationalFailureAsync(RetentionRecordClass recordClass, string scope, string reason, DateTimeOffset nowUtc, CancellationToken ct); }` (the latter uses the existing `OperationalCase` dedupe-key idiom, scoped `"retention"`).
- Produces: `RetentionModule.AddRetentionModule(IServiceCollection, Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase) : IServiceCollection`.

- [ ] **Step 1: Scaffold the project and register it in the solution**

Create `ReadyToGoTravel.Retention.csproj` matching `ReadyToGoTravel.Support.csproj`'s shape (`net10.0`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`). No `ProjectReference` to Booking/Support/Consumer — Retention is a leaf dependency; they reference it, never the reverse. Add project + test project to `ReadyToGoTravel.slnx`.

- [ ] **Step 2: Write failing policy-catalog tests against the full documented schedule**

In `RetentionPolicyCatalogTests.cs`, for every `RetentionRecordClass`, assert the exact period from `docs/security/data-retention-and-legal-hold.md`/OI-0011 (e.g. `GeneralSupportTicket` = 730 days, `SupportAttachment` = 90 days, `SecurityAuditRecord` = 730 days, `WebhookPayloadBody` = 90 days, `NotificationRenderedContent` = 90 days, `AbandonedCheckoutState` = 30 days, `CanonicalBookingEvidence` = 7 years, `SuccessfulSupplierPayload` = 90 days, `ExceptionalSupplierPayload` = 365 days). Cover: before expiry (`IsExpired` false one second before the boundary), exactly at expiry (boundary is inclusive — expired), after expiry (true), and that `CalculateExpiry` is a pure, deterministic function of `(recordClass, triggerAtUtc)` with no hidden `now` dependency.

- [ ] **Step 3: Write failing legal-hold domain and guard tests**

In `LegalHoldServiceTests.cs`, cover: opening a hold with at least one scope succeeds; opening a hold with zero scopes throws (rejects the "blanket hold" shortcut at construction time); `ExcludeHeldAsync` returns only candidates whose keys match an active scope's record class **and** populated key(s) — an unrelated `CustomerId`/record-class combination is never excluded; multiple overlapping holds on the same subject behave correctly (still held if *any* active matching scope exists); releasing a hold makes `IsHeldAsync` false immediately; a released-then-recreated hold on the same subject is held again; a hold with a past `ReviewByUtc` is still active (review date is advisory, not an automatic expiry) — assert this explicitly since it is a common misimplementation.

- [ ] **Step 4: Run the focused tests and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Retention.Tests/ReadyToGoTravel.Retention.Tests.csproj -c Release --filter FullyQualifiedName~Retention`

Expected: compilation failure because none of the types exist yet.

- [ ] **Step 5: Implement domain, persistence and the legal-hold service**

`LegalHold`/`LegalHoldScope`/`LegalHoldAuditEvent` are `internal sealed` with private setters and factory/behaviour methods only. `RetentionDbContext.HasDefaultSchema("retention")`; `legal_hold_audit_events` gets the identical append-only `SaveChanges` guard as `SupportTicketMessage`/`BookingVersion` (throw on `Modified`/`Deleted`), plus a PostgreSQL trigger in the migration (Task 1 owns an audit trail of compliance actions — treat it with the same rigor as booking version history). `legal_hold_scopes` has a `CHECK` constraint requiring at least one of `customer_id`/`component_booking_id`/`support_ticket_id` to be non-null (defence in depth alongside the domain-level guard in Step 3). Index `legal_hold_scopes` on `(record_class, customer_id)`, `(record_class, component_booking_id)`, `(record_class, support_ticket_id)` for the guard's lookup queries. `LegalHoldService` implements both `ILegalHoldGuard` and the create/release application methods consumed by the staff endpoints in Task 2.

- [ ] **Step 6: Run the focused tests and verify GREEN, then commit**

Run the Step 4 command again; confirm all pass.

```bash
git add src/ReadyToGoTravel.Retention tests/ReadyToGoTravel.Retention.Tests ReadyToGoTravel.slnx
git commit -m "feat(retention): add retention module skeleton, policy catalog and legal-hold domain"
```

---

### Task 2: Legal-hold-officer authorization and staff API

**Files:**
- Create: `src/ReadyToGoTravel.Retention/Http/LegalHoldOfficerAuthorization.cs`
- Create: `src/ReadyToGoTravel.Retention/Http/LegalHoldEndpoints.cs`
- Create: `src/ReadyToGoTravel.Retention/Http/RetentionContracts.cs`
- Modify: `src/ReadyToGoTravel.Api/Infrastructure/AuthenticationExtensions.cs`
- Modify: `src/ReadyToGoTravel.Api/Program.cs`
- Modify: `deploy/local/keycloak/rtgt-realm.json`
- Modify: `tests/ReadyToGoTravel.Api.Tests/*` (`TestAuthenticationHandler` gains the ability to set the `legal-hold-officer` role claim, exactly as it already fakes `support-agent`)
- Test: `tests/ReadyToGoTravel.Retention.Tests/LegalHoldEndpointsTests.cs`

**Interfaces:**
- Produces: `POST /api/v1/retention/legal-holds` (create; body: matter reference, reason, review date, scope entries), `GET /api/v1/retention/legal-holds` (list, filterable by active/released), `GET /api/v1/retention/legal-holds/{id}`, `POST /api/v1/retention/legal-holds/{id}/release` (body: release reason). All `RequireAuthorization("legal-hold-officer")`.

- [ ] **Step 1: Write failing authorization and endpoint tests**

Cover: an authenticated `consumer`-only principal gets `403` from every route; a `legal-hold-officer` principal can create, list, get and release; creating with zero scopes returns `400`; releasing an already-released hold is idempotent (no-op, `200`, does not throw); the create/release response never includes another customer's unrelated data; every create/release records a `LegalHoldAuditEvent` with the acting subject.

- [ ] **Step 2: Run the focused tests and verify RED**

- [ ] **Step 3: Implement the authorization handler and endpoints**

`LegalHoldOfficerAuthorizationHandler` copies `SupportAgentAuthorizationHandler`'s exact `realm_access`-parsing shape for role `"legal-hold-officer"`. Register the policy in `AuthenticationExtensions.AddPlatformAuthentication`. Add the `legal-hold-officer` realm role to `rtgt-realm.json` (mirroring the existing `support-agent` role entry). Wire `api.MapLegalHoldEndpoints()` into `Program.cs` after the existing support routes, behind the `"public-api"` rate limit (no new rate-limit policy needed — this is a low-volume staff surface).

- [ ] **Step 4: Run the focused tests and verify GREEN, then commit**

```bash
git add src/ReadyToGoTravel.Retention src/ReadyToGoTravel.Api tests deploy/local/keycloak/rtgt-realm.json
git commit -m "feat(retention): add legal-hold-officer role and staff API"
```

---

### Task 3: Booking-module retention sweeps (webhook body, notification content, abandoned checkout)

**Files:**
- Create: `src/ReadyToGoTravel.Booking/Retention/BookingRetentionSweepProcessor.cs`
- Create: `src/ReadyToGoTravel.Booking/Retention/IBookingRetentionSweepProcessor.cs`
- Modify: `src/ReadyToGoTravel.Booking/Webhooks/WebhookInboxItem.cs` (add internal `RedactRawBody(DateTimeOffset now)` — sets `RawBody = string.Empty`, leaves everything else untouched)
- Modify: `src/ReadyToGoTravel.Booking/Notifications/NotificationOutboxItem.cs` (add internal `RedactPayload(DateTimeOffset now)` — sets `PayloadJson = string.Empty`)
- Modify: `src/ReadyToGoTravel.Booking/Persistence/BookingEntityConfigurations.cs` (no schema change needed — both columns already exist and are mutable)
- Modify: `src/ReadyToGoTravel.Booking/BookingModule.cs` (register the processor + `RetentionOptions`)
- Modify: `src/ReadyToGoTravel.Booking/ReadyToGoTravel.Booking.csproj` (`ProjectReference` to `ReadyToGoTravel.Retention`)
- Modify: `src/ReadyToGoTravel.Worker/Worker.cs` (chain the new processor into `ExecuteCycleAsync`, gated on `RetentionOptions.Enabled`)
- Test: `tests/ReadyToGoTravel.Booking.Tests/BookingRetentionSweepTests.cs`

**Interfaces:**
- Produces: `IBookingRetentionSweepProcessor { Task<bool> ProcessCycleAsync(CancellationToken ct); }` — a single call sweeps all three Booking-owned record classes in one bounded pass (batch size 200 per class) and returns whether any work was performed (for the `didWork` chain).
- Consumes: `ILegalHoldGuard`, `IRetentionReceiptRecorder` from `ReadyToGoTravel.Retention`.

- [ ] **Step 1: Write failing sweep tests**

In `BookingRetentionSweepTests.cs` (SQLite in-memory fixture, matching `BookingDatabaseFixture`), cover:
- Webhook body: a `Completed` item with `CompletedAt` 91 days ago has `RawBody` cleared; one 89 days ago is untouched; a `Quarantined` item (any age) is never touched (no resolution timestamp exists — fails closed); `PayloadHash`/`CorrelationId`/`EventId`/`Status` survive unchanged.
- Notification content: a `Sent` item with `UpdatedAt` 91 days ago has `PayloadJson` cleared; a `Failed` item likewise; a `Pending`/`Processing` item is never touched; `CustomerId`/`Template`/`Severity`/`Locale`/`DeliveryReference`/`Status` survive unchanged.
- Abandoned checkout: a `CheckoutSession` in `AwaitingAcceptance` with `UpdatedAt` 31 days ago is deleted (whole aggregate, cascade); one in `ReadyForPayment` 31 days ago is deleted; one in `PaymentPending`/`BookingPending`/`Completed`/`RequiresSupport`/`Failed` — regardless of age — is **never** touched by this rule (only `AwaitingAcceptance`/`ReadyForPayment` are in scope); one 29 days old is untouched.
- Legal hold: a webhook item / notification item / checkout session with an active matching `LegalHoldScope` is excluded even though otherwise expired; an unrelated hold (different `ComponentBookingId`) does not suppress it; a hold created (fake `ILegalHoldGuard` returning held only after a mid-batch flag flips) between candidate selection and the per-item act still blocks that specific item (prove the immediately-before-action recheck, not just the batch-level filter).
- Idempotency: running the sweep twice in a row produces the same end state and does not throw or double-count in the receipt.
- Partial failure: one candidate in a batch of five throws (simulate via a poisoned row) — the other four still complete, and a `RetentionOperationalCase`-equivalent call is recorded for the failed one (assert via a fake `IRetentionReceiptRecorder`).

- [ ] **Step 2: Run the focused tests and verify RED**

- [ ] **Step 3: Implement the sweep processor**

Each of the three sweeps: (a) query up to 200 expired, not-yet-purged candidates ordered by trigger date; (b) call `ExcludeHeldAsync` once for the whole batch; (c) for each remaining candidate, re-check `IsHeldAsync` for that specific subject immediately before acting (closes the race window — see design doc); (d) act (`ExecuteUpdateAsync` for the two redaction sweeps; a per-item re-fetch-then-`Remove`+`SaveChangesAsync`, tolerating "already gone" as success, for checkout deletion); (e) catch per-item exceptions, call `RecordOperationalFailureAsync`, continue; (f) call `RecordAsync` once per record class per cycle with the batch's success/failure counts. `RetentionOptions.Enabled` (bound from `Booking:Retention:Enabled`, default absent/false) gates the whole processor at the top of `ProcessCycleAsync` — return `false` immediately when disabled.

- [ ] **Step 4: Run the focused tests and verify GREEN**

- [ ] **Step 5: Wire into the general worker and commit**

Add `IBookingRetentionSweepProcessor` to `Worker.ExecuteCycleAsync`'s `didWork |=` chain, same scope-per-cycle shape as the existing processors.

```bash
git add src/ReadyToGoTravel.Booking src/ReadyToGoTravel.Worker tests/ReadyToGoTravel.Booking.Tests
git commit -m "feat(retention): add Booking module retention sweeps for webhook, notification and abandoned checkout state"
```

---

### Task 4: Support-module retention sweeps (tickets, attachments, audit records)

**Files:**
- Create: `src/ReadyToGoTravel.Support/Retention/SupportRetentionSweepProcessor.cs`
- Create: `src/ReadyToGoTravel.Support/Retention/ISupportRetentionSweepProcessor.cs`
- Modify: `src/ReadyToGoTravel.Support/Domain/SupportAttachment.cs` (add internal `Purge(DateTimeOffset now)` — sets a new `ScanStatus.Purged` value or a dedicated `PurgedAtUtc` marker; keep `Id`/`TicketId`/`Sha256Checksum` for the receipt, clear `OriginalFileName`/`StorageKey`)
- Modify: `src/ReadyToGoTravel.Support/Domain/SupportAttachment.cs` (add `ScanStatus.Purged` to the enum)
- Modify: `src/ReadyToGoTravel.Support/Persistence/SupportEntityConfigurations.cs` (add `PurgedAtUtc` column)
- Modify: `src/ReadyToGoTravel.Support/SupportModule.cs` (register the processor)
- Modify: `src/ReadyToGoTravel.Support/ReadyToGoTravel.Support.csproj` (`ProjectReference` to `ReadyToGoTravel.Retention`)
- Modify: `src/ReadyToGoTravel.Worker/Worker.cs` (chain the new processor)
- Create: `src/ReadyToGoTravel.Support/Persistence/Migrations/20260810070000_Slice7SupportRetention.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/SupportRetentionSweepTests.cs`

**Interfaces:**
- Produces: `ISupportRetentionSweepProcessor { Task<bool> ProcessCycleAsync(CancellationToken ct); }`.

- [ ] **Step 1: Write failing sweep tests**

In `SupportRetentionSweepTests.cs`, cover:
- General ticket (`BookingReference` null/empty), `ClosedAt` 731 days ago: deleted entirely (ticket + messages + guest tokens + attachments + audit events, via existing cascade FKs where `Restrict` doesn't block it — verify the delete order handles the `Restrict` FKs from `support_audit_events`/`support_attachments` by purging attachments and audit rows first, then the ticket). One 729 days ago: untouched.
- Booking-related ticket (`BookingReference` non-empty), `ClosedAt` 731 days ago: **not** deleted by the general-ticket rule (must survive to the 7-year rule); confirm it is still present and unchanged.
- Booking-related ticket, `ClosedAt` 7 years + 1 day ago: deleted.
- Attachment: `TicketClosedAt` 91 days ago, `ScanStatus == Clean`: `IObjectStorage.DeleteAsync` called with the correct key, row transitions to `Purged`, `OriginalFileName`/`StorageKey` cleared, `Id`/`TicketId`/`Sha256Checksum` survive. One 89 days ago: untouched. A still-open ticket (no `ClosedAt`): its attachments are never touched by this rule regardless of attachment age.
- Attachment storage-delete failure: `IObjectStorage.DeleteAsync` throws — the row is left unchanged (not marked `Purged`) so the next cycle retries; an operational failure is recorded.
- Attachment storage-delete succeeds but the subsequent `SaveChangesAsync` fails (simulate) — next sweep cycle re-attempts `DeleteAsync` on the now-already-deleted key using a fake storage that tolerates a repeat delete as a no-op (proving the two-phase design is safe under this exact failure window from the task brief), and completes on retry.
- Security audit record: `SupportAuditEvent` older than 2 years is deleted; one linked to a ticket with an active legal hold is not; one newer than 2 years is not.
- Legal hold: an active hold scoped to a specific `SupportTicketId` protects that ticket's deletion/attachment purge even past its documented expiry; an unrelated hold (different ticket) does not.
- Idempotency and partial-batch failure, same shape as Task 3.

- [ ] **Step 2: Run the focused tests and verify RED**

- [ ] **Step 3: Implement the sweep processor and migration**

Ticket deletion order per candidate: purge attachments (storage + row) → delete audit events → delete guest tokens → delete messages → delete ticket, all inside one `SaveChangesAsync` per ticket (not one per row) so a mid-sequence failure leaves the ticket exactly as it was (no partial ticket). The booking-related classification reads `BookingReference` directly (already persisted, no cross-module lookup) — document in the outcome report that this uses ticket-closure-plus-7-years as the conservative implemented trigger, since `BookingReference` is free text rather than a validated foreign key and the referenced booking's own final-settlement date cannot be safely resolved from it. Migration adds `support_attachments.purged_at` (nullable) and the `Purged` enum value (string-backed, no schema change needed beyond the new value itself + the new column).

- [ ] **Step 4: Run the focused tests and verify GREEN**

- [ ] **Step 5: Wire into the general worker and commit**

```bash
git add src/ReadyToGoTravel.Support src/ReadyToGoTravel.Worker tests/ReadyToGoTravel.Support.Tests
git commit -m "feat(retention): add Support module retention sweeps for tickets, attachments and audit records"
```

---

### Task 5: Consumer-module account closure

**Files:**
- Modify: `src/ReadyToGoTravel.Consumer/Customers/Customer.cs` (add `ClosedAtUtc`, `Close(TimeProvider)`, idempotent)
- Modify: `src/ReadyToGoTravel.Consumer/Persistence/ConsumerEntityConfigurations.cs`
- Modify: `src/ReadyToGoTravel.Consumer/Http/ConsumerEndpoints.cs` (add `POST /customers/me/close`)
- Create: `src/ReadyToGoTravel.Consumer/Retention/ConsumerRetentionSweepProcessor.cs`
- Create: `src/ReadyToGoTravel.Consumer/Retention/IConsumerRetentionSweepProcessor.cs`
- Modify: `src/ReadyToGoTravel.Consumer/ConsumerModule.cs`
- Modify: `src/ReadyToGoTravel.Consumer/ReadyToGoTravel.Consumer.csproj` (`ProjectReference` to `ReadyToGoTravel.Retention`)
- Modify: `src/ReadyToGoTravel.Worker/Worker.cs`
- Create: `src/ReadyToGoTravel.Consumer/Persistence/Migrations/20260810080000_Slice7AccountClosure.cs`
- Test: `tests/ReadyToGoTravel.Consumer.Tests/AccountClosureTests.cs`
- Test: `tests/ReadyToGoTravel.Consumer.Tests/ConsumerRetentionSweepTests.cs`

**Interfaces:**
- Produces: `Customer.Close(TimeProvider) : DomainResult<Customer>` — idempotent (no-op success if already `Closed`); sets `Status = Closed`, `ClosedAtUtc = now`.
- Produces: `POST /api/v1/customers/me/close` (policy `consumer`; `204`; a second call is idempotent `204`).
- Produces: `IConsumerRetentionSweepProcessor { Task<bool> ProcessCycleAsync(CancellationToken ct); }` — minimises eligible profile fields for accounts closed 30+ days ago (grace window mirrors the shortest documented grace pattern used elsewhere for support-ticket promotion) **unless** the customer needs a full `IBookingProvider`-style ownership check against Booking (see Step 3 note on the dependency-inversion port).

- [ ] **Step 1: Write failing account-closure tests**

In `AccountClosureTests.cs` (Consumer only, no cross-module DB access — Booking-side "has evidence" is proven via a fake port, see Step 3): closing an active customer sets `Status`/`ClosedAtUtc`; closing an already-closed customer is a no-op returning success (does not overwrite `ClosedAtUtc`); `ConsumerBookingContextResolver.ResolveCustomerIdAsync` returns `null` for a closed customer even with a technically-valid subject (already covered by existing `Status == Active` filter — add a regression test proving it explicitly for the closure path); the HTTP endpoint returns `204` for both first and repeat calls and requires the `consumer` policy (no `[AllowAnonymous]`).

In `ConsumerRetentionSweepTests.cs`: a closed customer (30+ days) with **no** linked `ComponentBooking` in a non-abandoned status and **no** support ticket (via the fake port) has its mutable profile fields minimised (`PreferredLocale`/`DisplayCurrency` reset to defaults or a documented redacted marker — decide and assert one concrete behaviour) while `Id`/`Subject`/`Status`/`ClosedAtUtc` survive; a closed customer **with** a `Confirmed`/`Completed`/`Cancelled`/`RefundRequired`/`RequiresSupport` component booking or a support ticket is **not** minimised (only `Status`/`ClosedAtUtc` reflect closure — the stable identifier and profile needed to explain retained evidence are preserved); a customer closed less than 30 days ago is untouched; a closed-and-held customer (active `LegalHoldScope` on `RetentionSubjectKind.Customer`) is never minimised regardless of age.

- [ ] **Step 2: Run the focused tests and verify RED**

- [ ] **Step 3: Implement**

Define a small port `IConsumerRetentionEvidencePort { Task<bool> HasProtectedEvidenceAsync(Guid customerId, CancellationToken ct); }` **in the Consumer module** (Consumer defines the port it needs; Booking/Support implement adapters registered in DI from their own `Add*Module` methods — this is the same dependency-inversion shape used for `ILegalHoldGuard`, applied in the other direction so Consumer never takes a compile-time reference to Booking/Support). Booking's adapter checks for any `ComponentBooking` with `Status` in the non-abandoned set for the customer's checkouts; Support's is composed with it (any non-purged ticket). `ConsumerRetentionSweepProcessor` calls the port before minimising, exactly like the other sweeps call `ILegalHoldGuard`.

- [ ] **Step 4: Run the focused tests and verify GREEN**

- [ ] **Step 5: Wire into the general worker, register the Booking/Support adapters, and commit**

`AddBookingModule`/`AddSupportModule` each register their `IConsumerRetentionEvidencePort` adapter contribution; compose them behind a single `IConsumerRetentionEvidencePort` in the `Api`/`Worker` hosts (an aggregate implementation checking both, registered at the host composition root — the one place allowed to know about all modules at once, exactly where `Program.cs` already composes `AddConsumerModule`/`AddBookingModule`/`AddSupportModule` together).

```bash
git add src/ReadyToGoTravel.Consumer src/ReadyToGoTravel.Booking src/ReadyToGoTravel.Support src/ReadyToGoTravel.Api src/ReadyToGoTravel.Worker tests/ReadyToGoTravel.Consumer.Tests
git commit -m "feat(retention): add customer-initiated account closure and Consumer minimisation sweep"
```

---

### Task 6: Production-readiness certification catalog

**Files:**
- Create: `src/ReadyToGoTravel.Retention/Readiness/ReadinessGate.cs`
- Create: `src/ReadyToGoTravel.Retention/Readiness/ReadinessCatalog.cs`
- Test: `tests/ReadyToGoTravel.Retention.Tests/ReadinessCatalogTests.cs`

**Interfaces:**
- Produces: `enum ImplementationStatus { NotStarted, InProgress, Complete }`; `enum VerificationStatus { Unverified, LocallyVerified, ExternallyVerified }`; `enum ActivationState { Disabled, Enabled }`; `sealed record ReadinessGate(string Name, ImplementationStatus Implementation, VerificationStatus Verification, string RequiredEvidence, string ApprovalOwner, ActivationState Activation, bool Blocking, string Notes)`.
- Produces: `static class ReadinessCatalog { static IReadOnlyList<ReadinessGate> Gates; static bool Ready => !Gates.Any(g => g.Blocking && g.Verification != VerificationStatus.ExternallyVerified); }` (a gate that is `Blocking` must be externally verified — not merely locally/implementation-complete — before `Ready` can be true; this is deliberately strict so "implementation complete" can never masquerade as "production ready").

- [ ] **Step 1: Write failing readiness tests**

Cover: `Ready` is `false` while the catalog contains any `Blocking` gate with `Verification != ExternallyVerified` (seed the real current catalog and assert `Ready == false` today, since every production capability is genuinely still gated); a synthetic all-gates-passed catalog yields `Ready == true`; a single non-blocking outstanding gate does not affect `Ready`; every gate in the real catalog has non-empty `RequiredEvidence`/`ApprovalOwner` (no silently-approved gate).

- [ ] **Step 2: Run and verify RED, implement, verify GREEN**

Populate `ReadinessCatalog.Gates` directly from `docs/decisions/review-register.md`'s rows (OI-0002, OI-0003, OI-0005, OI-0006, OI-0011, Australian market legal pack, object-storage/ClamAV activation) plus two new Slice 7 gates: "Retention sweep production activation" (blocking: `false` — it is safe-by-default, not launch-blocking on its own, but recorded) and "Legal-hold-officer role provisioning" (blocking: `false` — an internal staffing/IAM action). Keep every `Notes` field honest and specific; do not mark anything `ExternallyVerified` that lacks recorded evidence.

- [ ] **Step 3: Commit**

```bash
git add src/ReadyToGoTravel.Retention/Readiness tests/ReadyToGoTravel.Retention.Tests
git commit -m "feat(retention): add production-readiness certification catalog"
```

---

### Task 7: Configuration, appsettings and worker wiring finalisation

**Files:**
- Modify: `src/ReadyToGoTravel.Api/appsettings.json`, `appsettings.Development.json`
- Modify: `src/ReadyToGoTravel.Worker/appsettings.json`, `appsettings.Development.json`, `Program.cs`
- Modify: `src/ReadyToGoTravel.Api/Program.cs` (register `AddRetentionModule`, connection-string resolution mirroring the Support precedent — try `ConnectionStrings:Retention`, fall back to `Consumer`)
- Test: `tests/ReadyToGoTravel.Api.Tests/*` startup/DI validation test extended to cover the new module registrations

- [ ] **Step 1: Add `Retention:Enabled` (default absent/false) and connection-string wiring to both hosts, matching the exact Support precedent**

- [ ] **Step 2: Run a full-solution build and the existing DI-validation/startup tests; fix any missing registration**

- [ ] **Step 3: Commit**

```bash
git add src/ReadyToGoTravel.Api src/ReadyToGoTravel.Worker tests/ReadyToGoTravel.Api.Tests
git commit -m "chore(retention): wire retention module configuration into API and Worker hosts"
```

---

### Task 8: Documentation

**Files:**
- Modify: `docs/plans/active/PLAN-0002-mvp-delivery.md` (mark Slice 7 complete; update Status/Change Log)
- Create: `docs/delivery/2026-08-10-slice-7-retention-privacy-production-readiness-outcome.md`
- Modify: `docs/security/data-retention-and-legal-hold.md` (record what is now implemented vs. still policy-only, without changing the approved schedule itself)
- Modify: `docs/decisions/review-register.md` (add the two new Slice 7 gates; update the Slice 7 status line)
- Create: `docs/operations/production-readiness-certification.md` (hand-authored mirror of `ReadinessCatalog`, kept honest by `ReadinessCatalogTests`)
- Modify: `docs/operations/mvp-operational-policy.md` (reference the now-implemented retention/legal-hold controls under "Operational Evidence Still Required" — move implemented items, leave external evidence items as still required)
- Create: `docs/domain/retention-and-legal-hold.md` (domain-doc companion matching the existing `docs/domain/*.md` convention, linked from `docs/domain/README.md`)
- Modify: `docs/domain/README.md`, `docs/domain/booking-reconciliation-and-version-history.md`, `docs/domain/support-tickets-and-guest-access.md`, `docs/domain/trips-travellers-and-bookings.md` (update "Retention" sections that currently say "unimplemented enforcement" now that Support/Booking sweeps exist)
- Modify: `docs/deployment/local-development.md` (document the `Retention:Enabled` opt-in flag and the retention/legal-hold local drill steps, matching the Support/MinIO/ClamAV opt-in documentation pattern)
- Modify: `docs/architecture/target-architecture.md` if the new module needs a one-line mention in "Logical Modules"

- [ ] Update every file above with content backed by actual implementation/test evidence gathered in Task 9. Do not mark PLAN-0002 Slice 7 complete until Task 9's full verification has actually passed.
- [ ] Run `bash scripts/validate-docs.sh` and fix any broken link/heading issue.
- [ ] Commit: `git commit -m "docs(retention): document Slice 7 retention, legal hold and production-readiness certification"`

---

### Task 9: Full regression verification, PostgreSQL drills, Docker builds and adversarial self-review

- [ ] `dotnet restore ReadyToGoTravel.slnx --locked-mode`
- [ ] `dotnet format ReadyToGoTravel.slnx --verify-no-changes --no-restore`
- [ ] `dotnet build ReadyToGoTravel.slnx -c Release --no-restore` (zero warnings, zero errors)
- [ ] `dotnet test ReadyToGoTravel.slnx -c Release --no-build` (full suite; record before/after count)
- [ ] `dotnet ef migrations has-pending-model-changes` for every `DbContext` (`ConsumerDbContext`, `BookingDbContext`, `SupportDbContext`, `RetentionDbContext`)
- [ ] `bash scripts/validate-docs.sh`
- [ ] `git diff --check`
- [ ] PostgreSQL 17 upgrade drill: fresh container, replay every migration in chronological order through the new Slice 7 migrations, confirm every new table/trigger/index exists and enforces (direct `UPDATE`/`DELETE` against `legal_hold_audit_events` raises the append-only error; the `legal_hold_scopes` CHECK constraint rejects an all-null scope row)
- [ ] Retention expiry drill against the live PostgreSQL container: seed rows spanning before/at/after expiry for each concretely-swept record class, run the sweep, confirm exactly the expired-and-unheld rows changed
- [ ] Legal-hold protection drill: seed an expired candidate, open a matching hold, run the sweep, confirm it is untouched; confirm an unrelated hold does not protect it
- [ ] Hold-release drill: release the hold, run the sweep again, confirm the now-still-expired record is purged within the documented post-release window
- [ ] Deletion/de-identification drill: confirm receipts are recorded and contain no raw personal content
- [ ] Docker builds: API, Web, general Worker, FlightReconciliation Worker
- [ ] Adversarial self-review of the entire `dev...HEAD` diff per the data-loss/privacy/concurrency/authorization/state-invariant checklist in the task brief; fix everything found and add regression tests before pushing
- [ ] Re-run every command above after any fix

## Completion Criteria

- Every concretely-scoped record class (Task 3-5) has a working, idempotent, legal-hold-respecting sweep with full test coverage.
- Every documented record class, including the policy-only ones, has calculator test coverage against the approved schedule.
- Legal hold creation/release is staff-only, fully audited, and provably does not suppress unrelated deletion.
- Account closure denies further customer access immediately and never deletes protected evidence.
- `ReadinessCatalog.Ready` is `false` today (accurately reflecting outstanding external gates) and the invariant that `Ready` requires no outstanding blocking gate is itself tested.
- Full regression suite passes; no Slice 1-6 test weakened or removed.
- All documentation accurately reflects what is implemented, what is policy-only, and what remains an external production gate.
