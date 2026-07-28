---
adr_id: ADR-0008
title: Durable Flight Reconciliation and Customer Notification
status: accepted
date_proposed: 2026-07-28
date_accepted: 2026-07-28
date_rejected: null
date_superseded: null
superseded_by: null
supersedes: []
decision_owners:
  - Product
  - Architecture
  - Operations
related_issues:
  - OI-0004
  - OI-0005
  - OI-0010
related_adrs:
  - ADR-0004
  - ADR-0005
  - ADR-0006
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
  - docs/operations/reliability-and-supportability.md
---

<!-- markdownlint-disable MD013 MD025 -->

# ADR-0008 — Durable Flight Reconciliation and Customer Notification

## Status

Accepted on 2026-07-28.

## Context

Customers need timely notice when a supplier-visible flight itinerary changes. Webhooks can reduce latency but cannot be assumed complete, ordered or sufficient, and a container restart must not lose scheduled checks. The platform must also preserve ADR-0005's distinction between fulfilment-supplier booking data and live operational flight status.

## Decision Drivers

- Detect meaningful changes even when a webhook is missing or delayed.
- Intensify checks as departure approaches.
- Preserve explainable, immutable change history under ADR-0004.
- Make scheduling durable, idempotent and observable across restarts.
- Isolate flight polling and notification pressure from general background work.
- Avoid representing booking reconciliation as live gate, terminal or movement data.

## Options Considered

### Option A — Dedicated durable flight-reconciliation worker

Run a private .NET 10 worker container with PostgreSQL-backed scheduled work, supplier retrieval, immutable version creation and notification triggers.

### Option B — General worker only

Run all flight reconciliation within the initial general-purpose worker deployment.

### Option C — Serverless timer functions or container crontab

Invoke flight checks from independently hosted serverless timers or an operating-system crontab inside a container.

## Recommendation

Choose Option A. Flight proximity scheduling, supplier quotas, failure isolation and customer notification justify a workload-specific worker under ADR-0006. Durable work records avoid the restart and observability weaknesses of an in-container crontab, while the container remains within the accepted .NET deployment baseline.

## Decision

Option A is accepted.

The platform deploys a dedicated private .NET 10 flight-reconciliation worker container. It accepts no public application traffic. PostgreSQL-backed durable work records and the selected background scheduling mechanism determine due work, prevent unsafe concurrent execution and preserve overdue work across restarts.

Every active future flight booking is reconciled at least once per calendar day outside the final 24 hours before scheduled departure. During the final 24 hours before each affected flight segment's scheduled departure, it is reconciled at least once per hour. Proximity checks stop after departure, cancellation or another terminal booking-lifecycle condition. Authenticated webhooks and pending-operation recovery can enqueue immediate reconciliation without waiting for the next scheduled check.

Each check retrieves the latest booking representation available from the fulfilment supplier and applies ADR-0004: map to the supplier-neutral canonical model, canonicalise, hash, compare, append an immutable version only for meaningful change, update current state transactionally and record an outbox event. Notification processing classifies and deduplicates the change, renders customer communication in the effective locale and records delivery attempts.

A retrieval failure records an inspectable failed attempt and never establishes that the itinerary is unchanged. Missed schedules become overdue observable work rather than disappearing. Supplier terms, account quotas and supported retrieval frequency remain production gates; if the supplier cannot support the minimum schedule, flight production readiness is blocked.

This worker reconciles booking, ticket and itinerary data. Gate, terminal, aircraft, diversion, delay and actual movement data remain outside this decision and require OI-0010 to select a separately validated operational source.

## Consequences

### Positive

- Webhook loss does not remove the scheduled safety net.
- Customers can be notified from meaningful, versioned booking changes.
- Proximity polling has an isolated scaling and failure boundary.
- Restarts and duplicate triggers do not silently lose or repeat work.

### Negative

- The initial deployment has another private worker workload to operate.
- Supplier rate limits and booking volumes require explicit capacity controls.
- Notification rules and canonicalisation changes require careful versioning.

### Risks

Supplier retrieval may omit or delay airline schedule changes. Polling too frequently may breach quotas or terms. Incorrect canonicalisation can produce duplicate or missed notifications. These risks require external LiteAPI evidence, capability-aware scheduling, idempotency, deterministic canonicalisation and operational alerting.

## Dependencies

[OI-0004](../../issues/open/OI-0004-flight-servicing-and-schedule-changes.md) and [OI-0005](../../issues/open/OI-0005-liteapi-webhook-coverage.md) remain in review for supplier evidence. [OI-0010](../../issues/open/OI-0010-operational-flight-status-provider.md) remains open and separate.

## Related Documents

- [ADR-0004](ADR-0004-booking-current-state-and-immutable-history.md)
- [ADR-0005](ADR-0005-reconciliation-and-operational-flight-status-boundary.md)
- [ADR-0006](ADR-0006-application-and-deployment-baseline.md)
- [Booking Reconciliation and Version History](../../domain/booking-reconciliation-and-version-history.md)
