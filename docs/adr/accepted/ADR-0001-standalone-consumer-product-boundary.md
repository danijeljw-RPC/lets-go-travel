---
adr_id: ADR-0001
title: Standalone Consumer Product Boundary
status: accepted
date_proposed: 2026-07-27
date_accepted: 2026-07-28
date_rejected: null
date_superseded: null
superseded_by: null
supersedes: []
decision_owners:
  - Product
  - Architecture
related_issues:
  - OI-0001
  - OI-0007
related_adrs: []
related_plans:
  - PLAN-0001
related_docs:
  - docs/product/vision-and-scope.md
  - docs/product/consumer-mvp-scope.md
---

<!-- markdownlint-disable MD013 MD025 -->

# ADR-0001 — Standalone Consumer Product Boundary

## Status

Accepted on 2026-07-28.

## Context

`readytogo.travel` needs a clear product identity before technical planning. The source pack describes a consumer trip and booking experience and explicitly rejects carrying over the assumptions of a larger travel platform.

## Decision Drivers

- Keep scope understandable for a small product and team.
- Optimise for individual customers and their travellers.
- Avoid enterprise structures that add design and implementation cost without an MVP need.
- Preserve the ability to add supplier and post-booking capability later.

## Options Considered

### Option A — Standalone consumer product

Model customer accounts, travellers, trips and bookings directly. Add another customer or partnership model only through a later product decision.

### Option B — General travel platform from the start

Build a general platform for several customer and operating models in anticipation of future expansion.

### Option C — Supplier-branded booking frontend

Make the product a thin user interface over LiteAPI models and capabilities.

## Recommendation

Choose Option A. It matches the stated product, removes unrelated complexity and still allows later supplier or product expansion through explicit boundaries.

## Decision

Option A is accepted. `readytogo.travel` is a standalone consumer product that models customer accounts, travellers, trips and bookings directly. Any later customer or partnership model requires a new product decision.

## Consequences

### Positive

- Smaller product and authorisation model.
- Clear customer/traveller ownership.
- Documentation and delivery planning stay focused.

### Negative

- Future business partnerships may require new domain and authorisation decisions.
- Features cannot assume enterprise reuse as a shortcut.

### Risks

Consumer scope can still expand too broadly unless changes follow the boundary recorded in closed [OI-0001](../../issues/closed/OI-0001-mvp-product-scope.md).

## Dependencies

Closed [OI-0001](../../issues/closed/OI-0001-mvp-product-scope.md) and [OI-0007](../../issues/closed/OI-0007-launch-market-locale-and-currency.md).

## Related Documents

- [Vision and Scope](../../product/vision-and-scope.md)
- [Consumer MVP Scope](../../product/consumer-mvp-scope.md)
