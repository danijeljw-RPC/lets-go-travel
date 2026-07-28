---
adr_id: ADR-0004
title: Booking Current State and Immutable History
status: accepted
date_proposed: 2026-07-27
date_accepted: 2026-07-28
date_rejected: null
date_superseded: null
superseded_by: null
supersedes: []
decision_owners:
  - Architecture
  - Data
related_issues:
  - OI-0004
  - OI-0011
related_adrs:
  - ADR-0002
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
  - docs/security/data-retention-and-legal-hold.md
---

<!-- markdownlint-disable MD013 MD025 -->

# ADR-0004 — Booking Current State and Immutable History

## Status

Accepted on 2026-07-28.

## Context

Customer and support workflows need queryable current booking state, while disputes, reconciliation and notifications need durable evidence of meaningful changes.

## Decision Drivers

- Efficient current-booking queries.
- Explainable and immutable history.
- Deterministic change detection.
- Supplier-neutral evidence with protected raw data where permitted.

## Options Considered

### Option A — Normalised current state plus immutable canonical versions

Maintain relational current state and append versions containing canonical snapshot, hash, metadata, flags and `DiffJson` when meaningful state changes.

### Option B — Store supplier payloads only

Persist each retrieved payload and derive current state/history on demand.

### Option C — Current state plus event log only

Store current rows and selected domain events without full canonical snapshots.

## Recommendation

Choose Option A. It supports ordinary queries and preserves enough structured evidence to explain supplier changes.

## Decision

Option A is accepted. The platform maintains normalised relational current state and appends an immutable canonical version containing the snapshot, hash, metadata, flags and `DiffJson` whenever meaningful booking state changes.

## Consequences

### Positive

- Fast customer/support queries.
- Append-only evidence and meaningful diffs.
- Notification and analytics queries avoid repeatedly parsing snapshots.

### Negative

- Canonicalisation and schema versioning require careful design.
- Current state and history must update transactionally.

### Risks

Volatile supplier fields can create false versions; overly aggressive normalisation can hide real changes. Raw payload retention may be restricted.

## Dependencies

Supplier retrieval freshness and the production contract/legal gates retained by closed [OI-0011](../../issues/closed/OI-0011-supplier-payload-retention.md). The selected retention baseline is [Data Retention and Legal Hold](../../security/data-retention-and-legal-hold.md).

## Related Documents

- [Booking Reconciliation and Version History](../../domain/booking-reconciliation-and-version-history.md)
- [Data Retention and Legal Hold](../../security/data-retention-and-legal-hold.md)
