---
adr_id: ADR-0005
title: Reconciliation and Operational Flight Status Boundary
status: accepted
date_proposed: 2026-07-27
date_accepted: 2026-07-28
date_rejected: null
date_superseded: null
superseded_by: null
supersedes: []
decision_owners:
  - Architecture
  - Product
related_issues:
  - OI-0004
  - OI-0010
related_adrs:
  - ADR-0004
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
---

<!-- markdownlint-disable MD013 MD025 -->

# ADR-0005 — Reconciliation and Operational Flight Status Boundary

## Status

Accepted on 2026-07-28.

## Context

Supplier booking state and live flight operations overlap in customer language but come from different sources with different accuracy, timing and obligations.

## Decision Drivers

- Avoid promising live status from booking data.
- Make supplier evidence and update limits explicit.
- Permit a specialist operational provider later.

## Options Considered

### Option A — Separate capabilities and sources

Reconcile booking/ticket/itinerary state from fulfilment suppliers. Obtain gate, terminal, delay, aircraft, diversion and actual movement from a separately validated operational source.

### Option B — Treat supplier booking retrieval as flight status

Use the latest retrieved booking as the complete source for customer flight updates.

### Option C — Do not reconcile flight bookings

Display only the original confirmed flight record and direct customers to airlines for changes.

## Recommendation

Choose Option A, while deferring live operational status from the MVP unless [OI-0010](../../issues/open/OI-0010-operational-flight-status-provider.md) establishes a justified provider and product need.

## Decision

Option A is accepted. Booking, ticket and itinerary state is reconciled from fulfilment suppliers. Gate, terminal, delay, aircraft, diversion and actual movement data requires a separately validated operational source and remains outside the MVP unless OI-0010 establishes the provider and product need.

## Consequences

### Positive

- Honest customer messaging and clearer data provenance.
- Booking reconciliation can proceed independently of operational tracking.

### Negative

- A richer flight experience may require another commercial integration.
- Multiple sources need correlation and conflict rules.

### Risks

If LiteAPI retrieval does not reflect schedule changes, booking reconciliation alone cannot notify customers of them.

## Dependencies

[OI-0004](../../issues/open/OI-0004-flight-servicing-and-schedule-changes.md) and [OI-0010](../../issues/open/OI-0010-operational-flight-status-provider.md).

## Related Documents

- [Booking Reconciliation and Version History](../../domain/booking-reconciliation-and-version-history.md)
