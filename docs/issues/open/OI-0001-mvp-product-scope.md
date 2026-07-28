---
issue_id: OI-0001
title: Decide the First Sellable Product Scope
status: open
type: product-question
priority: p0
severity: high
created: 2026-07-27
updated: 2026-07-27
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

Decide whether the first sellable release is hotel-first, hotels and flights, or trip planning without supplier booking.

## Context

The foundation describes both hotel and flight capability, but flight account access, Australian coverage, servicing and schedule-change behaviour are not yet verified.

## Options

### Option A — Hotel-first transactional MVP

Launch web-based hotel search, payment, booking and trip management. Continue flight validation separately.

### Option B — Hotels and flights together

Delay launch until both product categories meet coverage, payment, servicing and support requirements.

### Option C — Trip organiser before selling

Launch trips and manual itinerary management without supplier transactions, then add booking later.

## Recommendation

Choose Option A. It creates a sellable end-to-end path while avoiding a launch dependency on unresolved airline and flight-servicing questions.

## Evidence Required

- LiteAPI hotel sandbox proves search, prebook, payment, book, retrieve and cancel/refund paths needed by the MVP.
- Commercial terms permit the proposed Australian consumer use.
- Product confirms which trip and support features are essential at launch.

## Decision Impact

This issue controls product roadmap, domain depth, API surface, supplier validation and the first implementation plan.

## Acceptance Criteria

- [ ] One option is selected.
- [ ] In-scope and deferred capabilities are explicit.
- [ ] Launch success measures are defined.
- [ ] [Consumer MVP Scope](../../product/consumer-mvp-scope.md) is updated.

## Related Documents

- [Vision and Scope](../../product/vision-and-scope.md)
- [Capability Roadmap](../../product/capability-roadmap.md)
