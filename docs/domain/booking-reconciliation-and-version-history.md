<!-- markdownlint-disable MD013 -->

# Booking Reconciliation and Version History

## Purpose

Reconciliation retrieves the latest booking representation exposed by a supplier, maps it to the platform model, detects meaningful changes, updates current operational state and records immutable history.

The ordered process is: retrieve supplier state; map it into the canonical model; canonicalise and hash it; compare it with the latest version; append a version only for meaningful change; calculate metadata, flags and `DiffJson`; update current state; and initiate the relevant customer or support workflow.

## Current Normalised State

Relational current-state records support customer views, upcoming trips, support, notifications, cancellation eligibility and operational queries. They are not the history record.

## Immutable Versions

Create a new append-only version only when the deterministic canonical hash changes. Each version should retain booking ID, version number, observation time, effective time if known, source, canonicalisation version, canonical snapshot, hash, structured metadata, flags, `DiffJson`, correlation identifiers, and protected raw-payload evidence where retention is permitted.

## Canonicalisation

Canonicalisation must define property order, collection order, stable traveller/segment identity, null versus absent values, whitespace, number/currency representation, date/time/timezone handling, volatile supplier fields and supplier timestamps that should not create versions.

## Hash

The hash detects canonical changes and supports integrity checking. It does not replace the snapshot, authenticate the supplier, encrypt data or explain the change.

## Metadata, Flags and DiffJson

Metadata supports efficient classification by severity, affected travellers/segments, notification need, support need and automatic-action eligibility. Flags identify useful categories such as schedule, airport, status, room, price, policy, cancellation or refund change. `DiffJson` records stable object identity, semantic field, old/new values, change type and timezone/currency context.

Generated human-readable summaries may describe changes such as a later departure, changed airport, removed seat, changed hotel date or supplier cancellation, but remain derived views rather than historical evidence. Change severity may be informational, minor, material, critical or support-required; thresholds for timing, connections, downgrades, relocation, price and policy changes require explicit product rules.

## Trigger and Concurrency

Triggers may include webhooks, booking completion, scheduled checks, customer/support access and pending-operation recovery. A transaction and concurrency strategy must prevent webhook, worker and customer actions from overwriting newer state.

Polling frequency must respect supplier terms, quotas, event coverage, travel proximity and customer value. Concurrency control must account for competing workers, webhook/job races, cancellation during reconciliation, stale responses, out-of-order events and retry after a partial update.

Under [ADR-0008](../adr/accepted/ADR-0008-durable-flight-reconciliation-and-customer-notification.md), the dedicated flight-reconciliation worker checks every active future flight booking at least once per calendar day outside the final 24 hours before scheduled departure and at least once per hour during the final 24 hours before each affected flight segment. Authenticated webhooks and pending-operation recovery may enqueue immediate checks. Durable PostgreSQL work records preserve overdue work across restarts and prevent duplicate workers from processing the same due check unsafely.

A meaningful change updates current state and creates its immutable version in the same transaction, then records a notification event in the outbox. Notification handlers classify severity, deduplicate customer communication, use the effective customer locale and retain delivery attempts. A failed supplier retrieval creates an inspectable failure and never implies that no change occurred.

## Failure and Retention

A failed attempt records time, supplier, category, retry eligibility, next retry, correlation ID, last known state and whether customer/support action is required. Failure to reconcile never implies that the booking is unchanged.

Canonical history and raw supplier evidence have different retention periods under [Data Retention and Legal Hold](../security/data-retention-and-legal-hold.md). Canonical booking/financial evidence uses the seven-year baseline. Successful allowlisted raw supplier payloads expire after 90 days; ambiguous-operation, mapping-failure, incident or dispute payloads expire 12 months after resolution unless a legal hold or stricter requirement applies. [OI-0011](../issues/open/OI-0011-supplier-payload-retention.md) remains in review only for LiteAPI contractual terms and Australian legal/privacy validation.

## Critical Limit

Reconciliation detects only what the supplier exposes. It is separate from live operational flight status. See [ADR-0005](../adr/accepted/ADR-0005-reconciliation-and-operational-flight-status-boundary.md).
