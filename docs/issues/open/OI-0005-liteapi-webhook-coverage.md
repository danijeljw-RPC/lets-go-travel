---
issue_id: OI-0005
title: Validate LiteAPI Webhook Coverage and Delivery Guarantees
status: open
type: integration-question
priority: p0
severity: high
created: 2026-07-27
updated: 2026-07-27
decision_owners:
  - Architecture
  - Security
related_adrs:
  - ADR-0002
  - ADR-0004
related_plans:
  - PLAN-0001
related_docs:
  - docs/integrations/liteapi-nuitee-connect.md
  - docs/operations/reliability-and-supportability.md
blocked_by: []
---

# OI-0005 — Validate LiteAPI Webhook Coverage and Delivery Guarantees

## Summary

Validate the exact webhook event set, authentication, delivery, retry, ordering, replay and environment behaviour available to this project.

## Context

Official documentation describes hotel and flight lifecycle events, unique event IDs, an optional authentication token and configurable retries. It also notes some behaviours differ between sandbox and production. Account-level availability and schedule-change coverage remain unverified.

## Options

### Option A — Webhook-first with reconciliation safety net

Use authenticated webhooks to trigger durable inbox processing and supplier retrieval, plus scheduled reconciliation for missing events.

### Option B — Polling-first

Rely on scheduled retrieval and use webhooks only as an optimisation.

### Option C — Webhook-only

Assume successful event delivery is sufficient to maintain booking state.

## Recommendation

Choose Option A. It provides timely response without treating webhook delivery as complete or ordered. Reject webhook-only state mutation.

## Evidence Required

- Account event catalogue for hotels and enabled flights.
- Authentication mechanism and security review.
- Retry count/backoff, event retention and replay support.
- Ordering and duplication guarantees.
- Production-only event list and sandbox test limits.
- Evidence for amendment, cancellation, refund and schedule-change events.

## Decision Impact

Controls eventing, security, reconciliation frequency, operational alerts and support.

## Acceptance Criteria

- [ ] Event matrix and environment differences are recorded.
- [ ] Authentication and replay controls are approved.
- [ ] Inbox/deduplication/reconciliation behaviour is documented.
- [ ] Missing event coverage has an explicit polling or support fallback.

## Related Documents

- [LiteAPI/Nuitee Connect](../../integrations/liteapi-nuitee-connect.md)
- [Reliability and Supportability](../../operations/reliability-and-supportability.md)
