---
issue_id: OI-0005
title: Validate LiteAPI Webhook Coverage and Delivery Guarantees
status: in-review
type: integration-question
priority: p0
severity: high
created: 2026-07-27
updated: 2026-07-29
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
  - docs/evidence/liteapi/OI-0005-webhook-guarantees.md
blocked_by: []
---

<!-- markdownlint-disable MD013 MD025 -->

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

## Selected Direction

The product owner selected Option A on 2026-07-28. Authenticated webhooks are persisted to a durable inbox, deduplicated and processed asynchronously. They enqueue supplier retrieval and reconciliation rather than directly overwriting booking state. Scheduled checks cover active bookings and act as the missing-event safety net, including ADR-0008's flight schedule.

Public LiteAPI evidence now establishes shared-token authentication, configurable exponential-backoff retries, at-least-once delivery, possible duplicates, `event_id` deduplication and the sandbox marker. The issue remains in review only for account-specific subscriptions, actual retry configuration and controlled production delivery tests.

## Evidence Reviewed

- Product-owner webhook-first plus scheduled-fallback direction recorded on 2026-07-28.
- [Webhook Guarantees Evidence](../../evidence/liteapi/OI-0005-webhook-guarantees.md), verified against LiteAPI's current public webhook documentation.
- No account-specific event catalogue or replay/delivery guarantee has been recorded.

## Evidence Required

- Account event catalogue for hotels and enabled flights.
- Production authentication-secret configuration and compensating-control approval.
- Account retry count/backoff configuration and controlled retry observations.
- Production duplication, missing-event and environment-isolation tests.
- Production-only event list and sandbox test limits.
- Evidence for amendment, cancellation, refund and schedule-change events.

## Decision Impact

Allows durable inbox, authentication, deduplication, asynchronous retrieval and scheduled fallback implementation. It blocks only production reliance on untested account subscriptions and delivery behaviour.

## Acceptance Criteria

- [x] Public event matrix and environment differences are recorded.
- [x] Authentication, replay and shared-secret limitations are documented.
- [x] Inbox/deduplication/reconciliation behaviour is documented.
- [x] Missing event coverage has an explicit scheduled-reconciliation and support fallback.
- [ ] Account subscriptions, secrets, retries and controlled production deliveries are approved.

## Related Documents

- [LiteAPI/Nuitee Connect](../../integrations/liteapi-nuitee-connect.md)
- [Reliability and Supportability](../../operations/reliability-and-supportability.md)
- [Webhook Guarantees Evidence](../../evidence/liteapi/OI-0005-webhook-guarantees.md)
