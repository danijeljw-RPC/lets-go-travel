---
issue_id: OI-0001
title: Decide the First Sellable Product Scope
status: closed
type: product-question
priority: p0
severity: high
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Product
related_adrs:
  - ADR-0001
related_plans:
  - PLAN-0001
related_docs:
  - docs/product/consumer-mvp-scope.md
blocked_by: []
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0001 — Decide the First Sellable Product Scope

## Summary

The first sellable release supports hotel-only, flight-only and combined hotel-plus-flight customer journeys.

## Context

The product owner requires hotel, flight and hotel-plus-flight booking out of the box. Supplier capability and production evidence still control which offers may be exposed; closing the product-scope question does not close OI-0002 through OI-0006.

## Options

### Option A — Hotel-first transactional MVP

Launch web-based hotel search, payment, booking and trip management. Continue flight validation separately.

### Option B — Hotels and flights together

Delay launch until both product categories meet coverage, payment, servicing and support requirements.

### Option C — Trip organiser before selling

Launch trips and manual itinerary management without supplier transactions, then add booking later.

## Recommendation

Option B was selected on 2026-07-28. The product launches with all three customer entry paths, while flight inventory remains capability-gated so the platform never advertises an offer it cannot complete through an approved booking and payment route.

## Decision

Hotel-only, flight-only and combined hotel-plus-flight journeys are initial product scope. A combined journey may orchestrate and present separately fulfilled hotel and flight bookings. It does not by itself establish a regulated package, one supplier order, one customer contract, one price or one payment; those claims require separate commercial and legal evidence.

The product decision is closed. OI-0002 through OI-0006 remain production-readiness gates for LiteAPI commercial terms, airline capability, servicing, webhooks and payment.

## Evidence Required

- LiteAPI hotel sandbox proves search, prebook, payment, book, retrieve and cancel/refund paths needed by the MVP.
- Commercial terms permit the proposed Australian consumer use.
- Product confirms which trip and support features are essential at launch.

## Decision Impact

This issue controls product roadmap, domain depth, API surface, supplier validation and the first implementation plan.

## Acceptance Criteria

- [x] One option is selected.
- [x] In-scope and deferred capabilities are explicit.
- [x] Launch success requires a complete hotel, flight or combined journey using only offers with approved search, payment, booking and support routes.
- [x] [Consumer MVP Scope](../../product/consumer-mvp-scope.md) is updated through the decision-closeout documentation pass.

## Related Documents

- [Vision and Scope](../../product/vision-and-scope.md)
- [Capability Roadmap](../../product/capability-roadmap.md)
