---
issue_id: OI-0010
title: Decide the Operational Flight Status Boundary and Provider
status: open
type: wishlist
priority: p3
severity: low
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Product
  - Architecture
related_adrs:
  - ADR-0005
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
blocked_by: []
mvp_blocker: false
target_release: post-mvp
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

## Selected Direction

The product owner selected Option A on 2026-07-28. Live gate, terminal, delay, aircraft, diversion and actual-movement data is excluded from launch. The customer experience shows the latest reconciled booking and itinerary state and directs customers to the operating airline for live operational information.

This issue remains open as a post-MVP wishlist item so a dedicated operational provider can be evaluated later. It is explicitly not an MVP blocker and does not prevent implementation or launch planning. LiteAPI booking reconciliation under ADR-0008 continues independently and must not be presented as live operational status.

## Evidence Required

- Product need and notification use cases.
- Provider Australian coverage, latency, accuracy, licensing and cost.
- Identifier matching and conflicting-source rules.
- Customer disclaimer and support impact.

## Decision Impact

Controls flight UX, notifications, integration cost and customer promises.

## Acceptance Criteria

- [x] Live operational status is explicitly deferred from the MVP.
- [ ] Any selected provider has evidence for required markets and data.
- [x] Booking and operational data remain distinguishable in model and UI.
- [x] ADR-0005 already reflects the launch deferral and separate-provider boundary.

## Related Documents

- [ADR-0005](../../adr/accepted/ADR-0005-reconciliation-and-operational-flight-status-boundary.md)
