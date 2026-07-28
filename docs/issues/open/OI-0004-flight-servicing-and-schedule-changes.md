---
issue_id: OI-0004
title: Verify Flight Servicing and Schedule-Change Behaviour
status: in-review
type: integration-question
priority: p0
severity: critical
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Product
  - Architecture
related_adrs:
  - ADR-0004
  - ADR-0005
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
  - docs/integrations/liteapi-nuitee-connect.md
blocked_by:
  - OI-0003
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0004 — Verify Flight Servicing and Schedule-Change Behaviour

## Summary

Determine how flight bookings are retrieved and serviced after confirmation and whether airline schedule changes propagate through LiteAPI.

## Context

Documented create/list/retrieve operations do not prove source-current schedule-change data, voluntary/involuntary change handling, refund automation or manual escalation paths.

## Options

### Option A — Supplier supports full required servicing and current retrieval

Use supplier APIs/webhooks for changes, cancellations, refunds and booking reconciliation.

### Option B — Supplier supports booking but servicing is partly manual

Expose supported self-service actions and route unsupported cases to an explicit support workflow.

### Option C — Do not sell flights until another servicing model exists

Keep flights out of transactional scope.

## Recommendation

Plan for Option B as the likely minimum safe model, but do not sell flights until the exact manual/automated boundary and customer support path are contractually and technically proven.

## Selected Direction

The platform monitoring direction was selected on 2026-07-28 and accepted in [ADR-0008](../../adr/accepted/ADR-0008-durable-flight-reconciliation-and-customer-notification.md). A dedicated private .NET 10 worker reconciles active flight bookings daily, increases to hourly checks during the final 24 hours before each affected segment, records meaningful immutable itinerary versions under ADR-0004 and triggers customer notifications. Webhooks may trigger immediate checks.

This answers how the platform detects and records supplier-visible changes. It does not prove LiteAPI retrieval freshness, schedule-change propagation, exchanges, cancellation, refund automation or the supplier's manual servicing path. Option B remains the safe operational assumption until that evidence is recorded, so the issue is in review.

## Evidence Reviewed

- Product-owner flight-monitoring direction recorded on 2026-07-28.
- ADR-0004, ADR-0005 and ADR-0008.
- No controlled schedule-change retrieval test or written LiteAPI servicing matrix has been recorded.

## Evidence Required

- Latest-booking retrieval tests after controlled schedule changes if feasible.
- Written support for voluntary/involuntary changes, cancellation, refund and exchange.
- PNR, ticket and fulfilment ownership.
- Schedule-change event availability and payload.
- Manual servicing contacts, hours, SLA, fees and escalation process.

## Decision Impact

Blocks flight MVP, reconciliation promises, customer notifications, support staffing and cancellation/refund workflows.

## Acceptance Criteria

- [ ] Each servicing capability has supported/unsupported/manual status.
- [ ] Retrieval freshness and schedule-change propagation are evidenced.
- [ ] Manual escalation flow and owner are documented.
- [ ] Customer-facing limitations are approved.

## Related Documents

- [Booking Reconciliation and Version History](../../domain/booking-reconciliation-and-version-history.md)
- [ADR-0005](../../adr/accepted/ADR-0005-reconciliation-and-operational-flight-status-boundary.md)
