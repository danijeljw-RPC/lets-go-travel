<!-- markdownlint-disable MD013 -->

# Slice 5 Outcome — Booking Reconciliation and Notifications

## Outcome

Slice 5 is implemented on branch `codex/slice-5-booking-reconciliation` for review into `dev`. It delivers authenticated fail-closed webhook receipt, durable inbox processing, hotel and flight schedules, retrieval-driven immutable canonical booking versions, an owner-only history API and deduplicated customer-notification intents. Implementation completion does not activate a production supplier, webhook subscription or email provider.

The implementation follows the [Slice 5 design](../superpowers/specs/2026-08-08-slice-5-booking-reconciliation-design.md), [implementation plan](../superpowers/plans/2026-08-08-slice-5-booking-reconciliation.md), accepted [ADR-0004](../adr/accepted/ADR-0004-booking-current-state-and-immutable-history.md), [ADR-0008](../adr/accepted/ADR-0008-durable-flight-reconciliation-and-customer-notification.md) and [PLAN-0002](../plans/active/PLAN-0002-mvp-delivery.md).

## Authenticated Webhook Ingress

`POST /api/v1/webhooks/liteapi/{environment}` uses a dedicated rate limit and configured current/previous shared secrets without consumer JWT authentication. Disabled ingress returns `404`; invalid authentication, content type, body size, envelope or environment fails before persistence. Successful receipt stores the exact private body, SHA-256 hash, event identity and allow-listed correlation ID before `202`. The unique identity is `LiteAPI + environment + event_id`; an identical insert race or duplicate converges to one row, while a changed body under the same identity is quarantined and rejected.

Inbox processing treats supported events only as retrieval triggers. It parses the stringified nested payload behind the provider boundary, finds the opaque provider booking reference and enqueues reconciliation. Unsupported events, malformed payloads and missing/unmatched references remain inspectable without mutating customer booking state from the webhook. Transient scheduling failures retry durably, and expired processing leases are reclaimable.

## Scheduled Reconciliation and Convergence

Booking completion and pending-booking recovery create one schedule per component. Webhooks can bring that work forward. The general worker processes inbox, hotel and notification work; the dedicated flight worker processes flight work. Both use atomic conditional claims and bounded leases. Active hotels and future flights reconcile daily outside the final flight day; flights reconcile hourly inside the final 24 hours. Cancellation, failure, completion or departure stops future work.

Each attempt calls provider retrieval only; it never repeats booking, payment, settlement, cancellation or refund commands. Returned reference and product must match before state is mapped. Failure records a sanitised retry or operational case and never claims the supplier state was unchanged. Hotel, flight and combined-journey component states stay separate, while terminal supplier problems move the aggregate checkout to `RequiresSupport`.

## Immutable Canonical Versions and History

Retrieval maps into an explicit supplier-neutral hotel/flight state, canonicalises stable values and hashes deterministic JSON. The first successful retrieval creates version 1; an unchanged hash records the successful attempt without another version. A changed hash appends the next version with observation/effective time, source, canonicalisation version, snapshot, flags, stable diff, severity and correlation. Current component projection, version and notification intent commit together.

EF rejects version mutation or deletion. PostgreSQL migration `20260808010000_Slice5BookingReconciliation` adds unique version/hash boundaries and installs `booking.reject_booking_version_mutation()` to reject direct `UPDATE` or `DELETE`. `GET /api/v1/checkouts/{checkoutId}/history` checks immutable `sub` ownership and returns only ordered supplier-neutral metadata, flags and diffs; raw webhook bodies, snapshots, provider bindings/references, secrets, notification destinations and internal errors stay private.

## Customer Notifications

The classifier implements the approved informational, minor, material and travel-blocking policy, including the 30-minute flight-time boundary, airport/flight-number changes, room/inclusion/policy changes, hotel relocation, cancellation and failed confirmation. Minor email waits through 22:00–07:00 in the snapshotted customer timezone; material and travel-blocking email bypasses quiet hours. The outbox deduplicates by customer, component, version, channel and template and passes that key to the sender adapter for downstream idempotency.

The repository deliberately registers a disabled sender that leaves delivery retryable as `notification_capability_unavailable`. No vendor, destination-resolution or production email decision was invented. In-app history remains available independently of email delivery.

## Database and Configuration

The Booking schema adds version, reconciliation work/attempt, webhook inbox, notification outbox and operational-case tables, plus component projection and checkout locale/timezone fields. Checked-in API configuration disables webhook ingress. Checked-in Production configuration registers no provider or live notification sender. Worker configuration requires the Booking connection string and enables sanitized retrieval fixtures only in Development.

## Verification Record

Focused TDD covers canonical determinism and immutability, schedule cadence and lease recovery, webhook authentication/deduplication/processing, notification materiality/quiet hours/retry/lease recovery, owner isolation, provider-data exclusion, combined journeys and recovered-booking scheduling. Fresh completion checks produced:

- `dotnet restore ReadyToGoTravel.slnx --locked-mode`: passed;
- `dotnet format ReadyToGoTravel.slnx --no-restore --verify-no-changes`: passed;
- `dotnet build ReadyToGoTravel.slnx -c Release --no-restore`: passed with zero warnings and zero errors;
- `dotnet test ReadyToGoTravel.slnx -c Release --no-build --no-restore`: passed 215/215 tests across all seven test projects;
- `dotnet ef migrations has-pending-model-changes --project src/ReadyToGoTravel.Booking/ReadyToGoTravel.Booking.csproj --startup-project src/ReadyToGoTravel.Booking/ReadyToGoTravel.Booking.csproj --context BookingDbContext`: passed with no pending model changes;
- `bash scripts/validate-docs.sh`: passed for 109 Markdown files;
- `git diff --check`: passed; and
- API, web, general-worker and flight-reconciliation-worker Docker builds: passed.

## Production Gates and Exclusions

OI-0002 commercial/merchant allocation, OI-0003 carrier production capability, OI-0005 webhook guarantees and OI-0006 payment/PCI evidence remain open and authoritative. A configured local secret and passing sandbox fixtures do not prove production subscription, event coverage, retry/order behavior, supplier retrieval quotas, controlled delivery, a live notification route or operational exercises.

Slice 5 does not implement customer cancellation/refund commands, ticket servicing, live operational flight status, Slice 6 support tickets/guest links/attachments, Slice 7 retention enforcement/certification, native mobile, push/SMS or other deferred capabilities. The pull request must not be merged as part of this outcome.
