---
issue_id: OI-0003
title: Verify Australian Airline and Qantas Coverage
status: in-review
type: integration-question
priority: p0
severity: high
created: 2026-07-27
updated: 2026-07-29
decision_owners:
  - Product
  - Architecture
related_adrs:
  - ADR-0001
  - ADR-0002
related_plans:
  - PLAN-0001
related_docs:
  - docs/integrations/liteapi-nuitee-connect.md
  - docs/evidence/liteapi/OI-0003-australian-carrier-capability-review.md
blocked_by: []
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0003 — Verify Australian Airline and Qantas Coverage

## Summary

Verify whether the project's production account can search and book the Australian flight content required by the product, especially Qantas, Jetstar and Virgin Australia.

## Context

Official documentation describes generic flight APIs. It does not establish airline-by-airline production coverage, fare source or account eligibility.

## Options

### Option A — LiteAPI satisfies the Australian flight scope

Use LiteAPI for the verified carriers, markets and fare features.

### Option B — LiteAPI plus another flight supplier

Use supplier capability routing to fill meaningful coverage or servicing gaps.

### Option C — Defer flight selling

Launch hotel-first and keep flight content out of the transactional MVP.

## Recommendation

Proceed with Option A as a capability-gated integration. Implement carrier-neutral search and booking contracts, but do not advertise or enable a carrier in production until its entitlement and lifecycle evidence are recorded.

## Selected Direction

Option A is the selected initial route. On 2026-07-28, Qantas, Jetstar and Virgin Australia offers were observed through the LiteAPI API and console. This establishes search and search-fare capability for the tested account environment, routes, dates and point of sale. It is not proof of production entitlement, confirmed booking, ticketing or servicing.

The issue is in review while the coverage matrix and production-like searches are completed. Flight scope remains accepted under closed OI-0001, but customer-facing inventory must be capability-gated.

## Evidence Reviewed

- Product-owner observation of Qantas, Jetstar and Virgin Australia in the LiteAPI developer portal on 2026-07-28.
- [Australian Carrier Capability Evidence Review](../../evidence/liteapi/OI-0003-australian-carrier-capability-review.md), including public LiteAPI flight-access and sandbox limitations.
- No dated production-like search, booking, ticketing or servicing result has been recorded.

## Evidence Required

- Account-level confirmation of flight access.
- Sandbox and production-like searches for domestic/international Qantas, Jetstar and Virgin Australia.
- Content source such as GDS/NDC, fare families, ancillaries and frequent-flyer handling.
- Ticketing, cancellation, refund and servicing availability.
- Commercial availability for Australian point of sale and AUD.

## Decision Impact

Allows carrier-neutral and capability-gated flight implementation. It blocks only production enablement and carrier-support claims that lack dated production evidence.

## Acceptance Criteria

- [x] Coverage matrix records current search evidence and missing lifecycle evidence by carrier.
- [x] Unsupported or uncertain content is explicit.
- [x] OI-0001 remains flight-inclusive with capability-gated inventory.
- [x] Provider-neutral implementation may proceed without a second launch supplier.
- [ ] Production entitlement and dated end-to-end carrier evidence are recorded.

## Related Documents

- [LiteAPI/Nuitee Connect](../../integrations/liteapi-nuitee-connect.md)
- [Australian Carrier Capability Evidence Review](../../evidence/liteapi/OI-0003-australian-carrier-capability-review.md)
