<!-- markdownlint-disable MD013 -->

# Slice 5 Booking Reconciliation Design

## Status

Approved for autonomous implementation on 2026-08-08 by the request to complete PLAN-0002 Slice 5. This design refines the already accepted PLAN-0002, ADR-0004, ADR-0005, ADR-0006, ADR-0008, ADR-0009, ADR-0010 and OI-0005 direction. It does not approve production supplier, webhook, payment or email activation.

## Objective and scope

Implement authenticated LiteAPI webhook ingress, durable scheduled reconciliation, immutable supplier-neutral booking versions and durable customer-notification intents for hotel, flight and combined journeys. Webhook processing and scheduled retrieval must converge on the same current projection and append-only history without repeating a booking, settlement or payment operation.

Slice 6 support tickets and Slice 7 privacy/production certification remain excluded. A Slice 5 operational case is an internal recovery record only; it is not a customer support thread.

## Approaches considered

### Approach A — Coordinate Slice 5 inside the existing Booking feature module

Booking owns its current component projection and the transaction that appends a version. Put the webhook inbox, reconciliation schedule, immutable versions, operational cases and notification outbox in focused Booking subdomains, then expose narrow work-cycle interfaces to the existing general and flight workers.

This preserves one atomic persistence boundary for current state, history and notification intent. It also avoids having a new project query or mutate Booking's private tables.

### Approach B — Add separate Reconciliation and Notifications projects immediately

Separate projects would make product-module names explicit, but the MVP would need new versioned cross-module commands and distributed or shared-database transaction coordination before the first durable workflow exists. Allowing those projects to query Booking tables would directly violate ADR-0006 and ADR-0009.

### Approach C — Implement webhook handlers and timers directly in the API/workers

This is initially smaller, but it puts product and persistence rules in deployable hosts, makes tests harder, and encourages webhook-shaped mutation rather than retrieval-driven convergence.

## Selected approach

Choose Approach A. The deployable hosts remain composition roots only. The Booking project contains separate `Webhooks`, `Reconciliation` and `Notifications` namespaces with narrow public worker contracts. A later extraction remains possible after there is a measured scaling or ownership need and a versioned contract.

## Authenticated webhook ingress

Expose `POST /api/v1/webhooks/liteapi/{environment}` outside consumer JWT authentication and behind a dedicated rate-limit policy. Only `sandbox` and `production` are valid route values.

Configuration supplies an enabled flag, the endpoint environment, a current shared secret, an optional previous secret for bounded rotation and a maximum body size. Production remains disabled by default. Development may enable only the sandbox endpoint with an ignored secret.

The endpoint requires JSON, enforces the body-size limit while reading, compares the `authorization` header with configured secrets in constant time, parses only the top-level `event_id`, `event_name`, `request`, `response` and `sandbox` envelope, and rejects a route/payload environment mismatch. The token and unrestricted request headers are never persisted or logged.

After authentication and envelope validation, the exact body, SHA-256 hash, allow-listed correlation header, event identity and receipt time are inserted in the durable inbox. The database unique key is `LiteAPI + environment + event_id`. An identical duplicate returns success after confirming the stored hash; an identity collision with a different body is quarantined and rejected. A `2xx` acknowledgement occurs only after insert or identical-duplicate confirmation commits. Nested payload parsing, supplier retrieval and notification never extend request latency.

## Inbox processing and internal cases

The general worker claims inbox rows with a bounded lease. Known booking lifecycle events parse the nested stringified JSON defensively and extract an allow-listed external booking reference. They enqueue an immediate retrieval for the matching component booking. Unknown events, malformed nested payloads, missing correlation and references that cannot be matched create deduplicated internal operational cases and move the inbox row to completed, retryable or quarantined state as appropriate.

Internal retries use bounded exponential delays. The same inbox record is retried; replay never creates another inbox identity. Webhook payload values never update booking state directly.

## Provider-neutral retrieval contract

Extend the booking-provider retrieval result with an optional provider-neutral `RetrievedBookingState`. It contains only stable platform concepts: product, lifecycle status, customer-visible confirmation, hotel stay details, flight segments, inclusions/policies, currency/amount where supplied, and supplier observation time where reliable. Provider adapters translate provider payloads behind the integration boundary. Provider raw fields and event-specific payload shapes never enter public API contracts.

The existing fixture adapter returns deterministic retrieved states for confirmed hotel and active flight scenarios. Production provider registration remains absent.

## Durable reconciliation and schedules

Every component with a supplier booking reference has one durable schedule row. Initial booking completion/pending recovery creates or advances the row transactionally. Webhooks and internal reports only bring `due_at` forward; they cannot create a second booking or payment effect.

Both workers claim due work with an atomic conditional update and a bounded lease. The general worker processes hotel work and shared inbox/notification work. The dedicated flight worker processes only flight work. A crashed lease becomes claimable after expiry.

Each reconciliation retrieves the supplier booking exactly once for that attempt, validates that the returned reference and product match the component, maps it to the canonical platform model, and applies it transactionally. Retrieval failure records a sanitised attempt, increments retry state and never asserts that the booking is unchanged. Permanent mapping/reference failures create an operational case rather than blindly retrying.

After success, active hotels use a conservative daily next check. Active future flights are due daily outside the final 24 hours, hourly in the final 24 hours, and stop at departure, cancellation, failure or another terminal lifecycle state. An immediate webhook request can always advance an active row earlier. Live gate, terminal, delay, aircraft, diversion and movement state are absent.

## Immutable canonical versions

Canonicalisation uses a schema-versioned platform record and deterministic JSON: explicit property names, UTC timestamps, invariant decimals, sorted flight segments by stable identity and no volatile retrieval timestamps in the hashed snapshot. SHA-256 detects changes.

The first successful retrieval appends version 1 without a customer-change notification. A different canonical hash appends the next version, records observation/effective times, source, correlation ID, canonicalisation version, snapshot, hash, structured flags, severity metadata and stable `DiffJson`, then updates the current component projection in the same transaction. An unchanged hash records a successful attempt but appends no version.

Versions have no mutation methods. The EF context rejects modified or deleted version entries. The PostgreSQL migration also installs a trigger that rejects `UPDATE` and `DELETE` against the version table, preserving immutability outside EF. A unique `(component_booking_id, version_number)` key and unique `(component_booking_id, canonical_hash)` key make duplicate/concurrent convergence harmless.

An authenticated owner-only history endpoint returns supplier-neutral version metadata and diffs for a checkout. It never exposes raw webhook bodies, provider bindings, provider booking references, notification destinations or internal errors.

## Materiality and customer notifications

The diff classifier follows the approved operational policy:

- informational for confirmation or non-material instruction changes;
- minor for flight time movement under 30 minutes without a blocking state;
- material for time movement of at least 30 minutes, flight number/airport, hotel room/inclusion or cancellation-policy changes; and
- travel-blocking for cancellation, failed confirmation, relocation or supplier action required within 24 hours.

A meaningful version and its notification intent are committed together. The outbox key is unique per customer, component, version, channel and template version. Informational items remain in the in-app history and generate email only when the classifier marks email useful. Minor email waits through 22:00–07:00 in the configured customer timezone. Material and travel-blocking email bypasses quiet hours. Slice 5 stores the effective locale, timezone, severity, template/version, old/new version IDs and provider-neutral payload.

`ICustomerNotificationSender` is the outbound adapter. The worker records bounded attempts, success, retry or permanent failure without rolling back the booking version. No production sender is registered by default, so production activation still fails closed. Tests use an explicit recording sender. Selecting and provisioning a live email service is not invented by this slice.

## Security, concurrency and failure rules

- Customer ownership continues to derive from Keycloak `sub`; history lookup checks the immutable checkout customer ID through the Consumer application contract.
- Static webhook secrets are never logged, returned or stored in the inbox.
- Raw bodies are internal protected evidence and never returned by customer APIs.
- All external calls receive cancellation tokens.
- A claim lease prevents normal duplicate execution; unique hashes, version numbers, inbox identities, schedule rows, operational-case keys and notification keys remain the final idempotency boundary.
- Reconciliation never invokes `BookAsync`, payment preparation, settlement creation, capture, refund or cancellation commands.
- Combined journeys retain independent component schedules, current outcomes, versions and notification effects.

## Testing and verification

Tests cover constant-time shared-secret acceptance behavior through the endpoint, missing/invalid secret rejection, environment mismatch, body/content-type validation, identical duplicates, conflicting duplicates, durable acknowledgement, inbox replay, known and unsupported event handling, missed-webhook scheduled recovery, lease claiming, restart/expiry, hotel and flight cadence, terminal stop conditions, canonical determinism, unchanged-state no-op, immutable append-only versions, concurrent duplicate convergence, semantic severity, quiet hours, notification deduplication/retry, owner isolation, provider-data exclusion, cancellation propagation and existing Slice 1–4 regressions.

Repository completion checks remain locked restore, formatting, warning-as-error Release build, the full solution test suite, EF pending-model parity, documentation validation, `git diff --check`, applicable configuration validation and all four container builds.

## Production gates

OI-0002, OI-0003, OI-0005 and OI-0006 remain unchanged. In particular, implementation does not prove account subscriptions, real retry settings, controlled production delivery, carrier retrieval coverage, merchant responsibility, PCI scope or a live email-provider route. Production webhook ingress and outbound notification delivery stay disabled until their environment-specific configuration and approvals exist.

## Self-review record

- Placeholder scan: no TBD or unresolved implementation placeholder remains.
- Consistency: durable receipt, retrieval authority, scheduling, versions and notifications match PLAN-0002 and ADR-0004/0006/0008.
- Scope: support tickets, retention enforcement, live flight operations and production certification remain excluded.
- Ambiguity: initial versions do not notify, duplicate identities require matching hashes, worker ownership is explicit and production transports default off.
