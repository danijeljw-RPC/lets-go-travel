---
issue_id: OI-0010
title: Decide the Operational Flight Status Boundary and Provider
status: open
type: integration-question
priority: p2
severity: medium
created: 2026-07-27
updated: 2026-07-27
decision_owners:
  - Product
  - Architecture
related_adrs:
  - ADR-0005
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
blocked_by:
  - OI-0001
  - OI-0004
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0010 — Decide the Operational Flight Status Boundary and Provider

## Summary

Decide whether live gate, terminal, delay, aircraft, diversion and actual movement data belongs in the product and, if so, select a verified provider.

## Context

Booking suppliers may expose itinerary or schedule changes but should not be assumed to provide complete live operations.

## Options

### Option A — Defer operational status

Show booking itinerary/state and clearly direct customers to the airline for live operations.

### Option B — Add a dedicated operational provider

Integrate a specialist source with provenance, freshness, coverage and conflict rules.

### Option C — Use LiteAPI booking data as live status

Treat retrieved booking information as sufficient operational data.

## Recommendation

Choose Option A for the MVP. Evaluate Option B only after flights are in scope and customer value justifies the cost. Reject Option C without explicit vendor evidence.

## Evidence Required

- Product need and notification use cases.
- Provider Australian coverage, latency, accuracy, licensing and cost.
- Identifier matching and conflicting-source rules.
- Customer disclaimer and support impact.

## Decision Impact

Controls flight UX, notifications, integration cost and customer promises.

## Acceptance Criteria

- [ ] MVP include/defer decision is explicit.
- [ ] Any selected provider has evidence for required markets and data.
- [ ] Booking and operational data remain distinguishable in model and UI.
- [ ] ADR-0005 reflects the result.

## Related Documents

- [ADR-0005](../../adr/accepted/ADR-0005-reconciliation-and-operational-flight-status-boundary.md)
