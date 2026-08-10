<!-- markdownlint-disable MD013 -->

# Slice 7 Retention, Legal Hold, Privacy Operations and Production-Readiness Certification Design

## Status

Approved for implementation on 2026-08-10 by the request to complete PLAN-0002 Slice 7. This design implements the already-approved baseline in [Data Retention and Legal Hold](../../security/data-retention-and-legal-hold.md), closed [OI-0011](../../issues/closed/OI-0011-supplier-payload-retention.md), closed [OI-0008](../../issues/closed/OI-0008-saved-traveller-and-passport-data.md), [ADR-0009](../../adr/accepted/ADR-0009-mvp-application-contract-and-module-foundation.md) and [ADR-0010](../../adr/accepted/ADR-0010-mvp-runtime-storage-and-delivery-baseline.md). It does not invent a new retention policy, does not claim any outstanding production gate is satisfied, and does not open any new product-scope question.

One material decision was referred to the product owner before design: who may create and release a legal hold, and whether legal-hold administration needs API exposure. The answer (2026-08-10): add a new staff-only `legal-hold-officer` Keycloak role and a staff-only API, mirroring the Slice 6 `support-agent` console pattern exactly. This design implements that decision.

## Objective and scope

Implement the retention/expiry engine, legal-hold model, privacy minimisation and account-closure operations, and a production-readiness certification catalog described in PLAN-0002 Slice 7. Preserve every accepted Slice 1-6 boundary; introduce no new deployable container; fabricate no missing production evidence.

## Reconnaissance findings that shape this design

- The claim-lease `ExecuteUpdateAsync` pattern (webhook inbox, reconciliation work, notification outbox, attachment scan work) exists three times in the codebase and is the established idiom for concurrent durable work, but retention candidate selection is different in kind: expiry is *recomputed live* from stable source timestamps (`ClosedAt`, `CompletedAt`, `UpdatedAt` at a genuine terminal transition) every sweep, not drawn from a pre-populated queue row. A queue table would need to be kept in lock-step with every possible trigger event across three modules for no correctness benefit, and would reintroduce the exact "lost work between cycles" risk durable queues exist to prevent, for a job that is naturally idempotent by recomputation. This design therefore uses **stateless, idempotent, live-recomputed sweeps** (bounded batch, per-item isolation, no lease) rather than a fourth queue table family. Every mutation a sweep performs is itself an idempotent conditional bulk operation (`ExecuteUpdateAsync` with a WHERE clause that only matches not-yet-purged rows, or a per-item existence re-check immediately before a hard delete), so two workers running the same sweep concurrently, or a crash mid-sweep, cannot corrupt state or double-act — the next cycle simply finds the same or a smaller candidate set. This is also what makes the "reapply tombstones after restore" requirement trivial: a restored environment resumes sweeping automatically on its next worker cycle, because expiry is derived from already-historical trigger timestamps, not from a "have I already processed this" flag that a restore could roll back.
- `BookingVersion` (`booking.booking_versions`) is enforced append-only by both an EF `SaveChanges` guard **and** a PostgreSQL `BEFORE UPDATE OR DELETE` trigger (`booking.reject_booking_version_mutation()`). No later mechanism in this slice bypasses that trigger. The 7-year canonical-evidence de-identification action therefore cannot be implemented as an in-place mutation of `booking_versions` without either weakening the trigger or introducing a version-of-the-version redaction mechanism — a real architectural decision this slice does not make unilaterally. See "Explicitly scoped out" below.
- A repository-wide search confirms no date-of-birth, passport or identity-document field is persisted anywhere in the schema today (`TravellerSnapshot` carries only name/minor-status; OI-0008's reusable sensitive-traveller profile was deliberately never built; a regression test, `TravellerTableContainsNoSensitiveReusableColumns`, already guards this). The 90-day post-travel traveller-minimisation rule therefore has no concrete sensitive field to act on yet.
- No raw-supplier-payload evidence table exists (production supplier integration remains disabled per OI-0002/OI-0003/OI-0006; sandbox fixtures do not persist raw payloads outside the webhook inbox). The successful/exceptional raw-supplier-payload record classes are implemented as policy-and-calculator only.
- `Customer.Status` already declares a `Closed` enum member and `ConsumerBookingContextResolver` already filters booking-context resolution to `Status == Active` — but no code path ever sets `Closed`, and no `ClosedAt` field exists. Slice 7 is the first slice to wire this up.
- No compliance/legal authorization role exists (`AuthenticationExtensions` defines only `consumer` and `support-agent`).

## Approaches considered

### Approach A — Cross-cutting `ReadyToGoTravel.Retention` feature module owning legal hold, policy and readiness; each existing module owns its own sweep

A new feature module, same shape as Booking/Consumer/Support (own `retention` PostgreSQL schema, own `RetentionDbContext`), owns exactly the concepts that are genuinely cross-module: legal holds and their scopes, the retention policy catalog (trigger/period definitions and pure calculator functions), centralised deletion receipts, and the production-readiness catalog. It exposes narrow public interfaces (`ILegalHoldGuard`, `IRetentionReceiptRecorder`) that Booking, Support and Consumer depend on (project reference to Retention only — never the reverse), exactly mirroring the existing Support→Consumer "call the public contract, never the private tables" precedent from Slice 6. Each owning module implements its **own** sweep against its **own** schema (Booking sweeps webhook/notification/abandoned-checkout; Support sweeps tickets/attachments/audit; Consumer implements account closure), calling `ILegalHoldGuard` before every destructive action and `IRetentionReceiptRecorder` after every batch.

This keeps "a module owns its tables; another module cannot query its private tables directly" (ADR-0009) intact: Retention never touches Booking/Support/Consumer's schemas, and they never touch `retention.*` directly.

### Approach B — Fold legal hold and retention entirely into each existing module (no new module)

Duplicate a `LegalHold`-equivalent table into Booking, Support and Consumer schemas separately. Rejected: a hold's scope explicitly spans customers, bookings and tickets at once (the retention doc requires this), so a single authoritative hold record with cross-module visibility is required; three independently-evolving copies would drift and cannot be released atomically.

### Approach C — Generic rules-engine retention framework

A fully generic, data-driven policy/rule evaluator (e.g. expression trees or a rules DSL persisted in the database) that could express arbitrary future record classes without a code change. Rejected as over-engineering: the repository instruction is explicit that a generic rules engine is not called for, and the documented schedule is a fixed, known table of record classes, not an open-ended set. A `RetentionRecordClass` enum plus an explicit static policy catalog is simpler, compiler-checked, and directly testable against the documented schedule.

## Selected approach

Approach A. `ReadyToGoTravel.Retention` becomes a sixth feature project, wired into `ReadyToGoTravel.Api` (legal-hold staff endpoints) and `ReadyToGoTravel.Worker` (no new deployable container; the general worker gains retention-sweep cycles exactly as it gained webhook/reconciliation/notification/attachment-scan cycles in Slices 5 and 6).

## Retention record classes and concrete enforcement scope

| Record class | Concrete sweep in this slice | Rationale |
| --- | --- | --- |
| Booking-related support ticket (7y from closure; ticket has a non-empty `BookingReference`) | Yes, in Support | Real table, real field, precise trigger |
| General support ticket (2y from closure) | Yes, in Support | Real table, real field, precise trigger |
| Support attachment (90d from **ticket** closure, not attachment creation) | Yes, in Support | Real table; storage deletion via existing `IObjectStorage.DeleteAsync` |
| Security/audit record (2y from event; `SupportAuditEvent`) | Yes, in Support | Real table |
| Webhook payload body (90d from successful processing, `Status == Completed` only) | Yes, in Booking | Real column (`WebhookInboxItem.RawBody`); quarantined items are excluded (no resolution timestamp exists yet — fails closed rather than guessing) |
| Notification rendered content (90d from terminal delivery attempt) | Yes, in Booking | Real column (`NotificationOutboxItem.PayloadJson`); `CustomerId` already persists independently as the required "recipient reference" |
| Search/abandoned checkout state (30d inactivity, never past `ReadyForPayment`) | Yes, in Booking | Real table (`CheckoutSession`); scoped strictly to pre-payment status so nothing that touched a supplier/payment is ever swept by this rule |
| Account closure (customer-initiated; deny access, minimise eligible profile fields) | Yes, in Consumer | `Customer.Status`/`ClosedAt` newly wired per this slice |
| Canonical booking/financial/version evidence (7y "later of" trigger) | Policy + calculator only, no live sweep | `BookingVersion` is append-only at the PostgreSQL trigger level; no personal field exists on the mutable `ComponentBooking`/`CheckoutSession` columns to minimise; no real data will reach a 7-year threshold in this MVP. De-identifying the append-only table would require either weakening the Slice 5 immutability trigger or a new version-of-the-version redaction mechanism, which is a materially new architectural decision this design does not make unilaterally, consistent with "follow the existing design rather than bypassing immutability controls." |
| Successful / exceptional raw supplier payload | Policy + calculator only, no live sweep | No persistence table exists; production supplier integration remains disabled |
| Traveller DOB/contact minimisation (90d post-travel) | Policy + calculator only, no live sweep | No DOB/passport/contact field is persisted anywhere yet (confirmed by repository search and an existing regression test) |
| Diagnostic application logs (90d) | Not application code | Log-platform lifecycle (Azure Monitor), external infrastructure control |
| Ordinary backup rotation (35d) | Not application code | Managed PostgreSQL/object-storage infrastructure control; the *reapplication* half is satisfied by the live-recomputation sweep design above |

Every record class — including the policy-only ones — gets a `RetentionPolicyCatalog` entry and pure-function calculator test coverage against the documented schedule, so the trigger/expiry arithmetic for the full approved baseline is verified even where no live sweep exists yet.

## Legal hold model

`LegalHold` (matter reference, reason, authorised owner subject, created/review/released timestamps, release reason, release authority subject) plus one-or-more `LegalHoldScope` rows per hold. A scope row names a `RetentionRecordClass` and **at least one** of `CustomerId` / `ComponentBookingId` / `SupportTicketId` (never all-null — an all-null scope would be exactly the forbidden "any hold exists, block everything" shortcut). A candidate record is held only if an active (unreleased) scope row exists whose record class matches and whose populated key(s) match the candidate's own keys. This makes "an unrelated hold must not suppress deletion" a structural property of the query, not a convention.

Every sweep re-checks `ILegalHoldGuard` for each individual candidate **immediately before** acting on it (not once at batch start), closing the race window where a hold is created or released while a batch is in flight. Hold creation, release and every guard check are recorded to `legal_hold_audit_events` (append-only, same `SaveChanges`-guard pattern as `SupportTicketMessage`/`BookingVersion`).

Authorization: a new `legal-hold-officer` Keycloak realm role, a new `LegalHoldOfficerAuthorizationHandler` (structurally identical to `SupportAgentAuthorizationHandler`), and staff-only endpoints under `/api/v1/retention/legal-holds`. Never exposed to the `consumer` policy.

## Account closure

`Customer.Close(TimeProvider)`: sets `Status = Closed`, `ClosedAtUtc = now`. `ConsumerBookingContextResolver` already filters on `Status == Active`, so booking-context resolution for a closed account fails immediately without any further change — this is the concrete, already-proven mechanism satisfying "must not leave normal customer access active." A new `consumer`-policy self-service endpoint (`POST /api/v1/customers/me/close`) lets a customer close their own account; no new authorization surface is introduced (ownership is the existing subject-derived pattern used everywhere else in Consumer). Keycloak-side credential/session termination remains outside this repository's control (Keycloak owns credentials and sessions per `docs/security/identity-and-access.md`); the application-layer denial above is the concrete, testable control this repository can and does own.

On closure, a Consumer-owned sweep minimises eligible profile fields (display-derived contact fields) after the standard grace window, **unless** the customer has any `ComponentBooking` in a non-abandoned status (`Confirmed`, `Completed`, `Cancelled`, `RefundRequired`, `RequiresSupport`) or any support ticket, in which case the stable `Customer.Id`/`Subject` linkage is preserved exactly as the retention doc requires ("stable internal traveller/booking identifiers preserve evidence relationships") and only the closure state itself changes.

## Production-readiness certification

A `ReadinessCatalog` inside the Retention module: one `ReadinessGate` record per row of `docs/decisions/review-register.md` plus the new gates this slice introduces (retention-sweep activation, legal-hold-officer role provisioning). Each gate carries `ImplementationStatus`, `VerificationStatus`, `RequiredEvidence`, `ApprovalOwner`, `ActivationState` and `Blocking`. A pure-function `ReadinessCatalog.OverallResult()` computes `Ready` only when no `Blocking` gate is outside `GatePassed` — a fail-closed invariant directly unit-tested ("ready implies no mandatory gate outstanding"). No HTTP endpoint is added for this (it is an internal reporting/testing concern, not a runtime capability, and adding a new authorization boundary for it is not required by any accepted decision); a hand-authored markdown mirror is published as `docs/operations/production-readiness-certification.md` and kept honest by a test asserting the catalog's blocking-gate count matches the documented figure.

## Explicitly scoped out (documented limitation, not a missing requirement)

- Live de-identification of `booking_versions` at the 7-year threshold (append-only trigger; no real data will reach this threshold in the MVP lifetime; requires a future architectural decision about how immutable evidence is ever redacted).
- Traveller DOB/passport/contact minimisation sweep (no field exists to act on; OI-0008 reusable storage was never built).
- Raw successful/exceptional supplier-payload sweeps (no persistence table; production supplier integration disabled).
- Diagnostic log retention and ordinary backup rotation (infrastructure-owned, not application code); recorded as external-evidence-required gates in the readiness catalog.

## Activation posture

`Retention:Enabled` defaults to `false` in both checked-in Production and Development `appsettings.json`, matching the exact precedent set by `Support:Storage:Enabled` / `Support:Scanning:ClamAv:Enabled` in Slice 6 — implemented and fully tested, but not silently active, requiring explicit operational opt-in (documented in the local runbook) once verified. Legal-hold administration is not gated by this flag (it is protective, not destructive, and staff-authorization-gated).
