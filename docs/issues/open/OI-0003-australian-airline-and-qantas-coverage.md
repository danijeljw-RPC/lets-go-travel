---
issue_id: OI-0003
title: Verify Australian Airline and Qantas Coverage
status: open
type: integration-question
priority: p0
severity: high
created: 2026-07-27
updated: 2026-07-27
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
blocked_by: []
---

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

Choose Option C as the planning default until production-level evidence supports A or B. Do not advertise Qantas or other carrier support based on generic API documentation.

## Evidence Required

- Account-level confirmation of flight access.
- Sandbox and production-like searches for domestic/international Qantas, Jetstar and Virgin Australia.
- Content source such as GDS/NDC, fare families, ancillaries and frequent-flyer handling.
- Ticketing, cancellation, refund and servicing availability.
- Commercial availability for Australian point of sale and AUD.

## Decision Impact

Controls MVP scope, supplier strategy, UI claims, support and roadmap.

## Acceptance Criteria

- [ ] Coverage matrix records evidence by carrier, market and capability.
- [ ] Unsupported or uncertain content is explicit.
- [ ] OI-0001 is updated with the result.
- [ ] Supplier fallback need is decided.

## Related Documents

- [LiteAPI/Nuitee Connect](../../integrations/liteapi-nuitee-connect.md)
