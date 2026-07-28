---
plan_id: PLAN-0001
title: Project Planning Readiness
status: active
owner: Product and Architecture
created: 2026-07-27
updated: 2026-07-28
related_issues:
  - OI-0001
  - OI-0002
  - OI-0003
  - OI-0004
  - OI-0005
  - OI-0006
  - OI-0007
  - OI-0008
  - OI-0009
  - OI-0010
  - OI-0011
  - OI-0012
related_adrs:
  - ADR-0001
  - ADR-0002
  - ADR-0003
  - ADR-0004
  - ADR-0005
  - ADR-0006
  - ADR-0007
related_docs:
  - docs/product/consumer-mvp-scope.md
  - docs/integrations/liteapi-nuitee-connect.md
  - docs/architecture/target-architecture.md
scope:
  - docs
  - product-discovery
  - supplier-validation
---

# PLAN-0001 — Project Planning Readiness

## Status

Active. The documentation baseline is built; external evidence and owner decisions remain before implementation planning.

## Purpose

Turn the consumer travel foundation into an evidence-backed set of product and architecture decisions that is specific enough to produce the first implementation plan safely.

## Background

The repository now contains a compact consumer-project documentation system. All seven initial ADRs are accepted. Twelve open issues capture the remaining product, vendor, commercial, payment, privacy and operational evidence and decisions with options and recommendations.

## Scope

### In Scope

- Decide the first sellable product and launch market.
- Obtain LiteAPI commercial, hotel, flight, servicing, webhook and payment evidence.
- Decide traveller data and support policies.
- Maintain the accepted ADR baseline as open issues and supplier evidence are resolved.
- Produce an implementation-ready MVP scope and dependency map.

### Out of Scope

- Application implementation.
- Database schema, endpoint or infrastructure design beyond decision-level architecture.
- Production supplier onboarding.
- Native mobile delivery.
- Live operational flight-status integration unless separately approved.

## Inputs

- Root foundation pack.
- Structured documentation under `docs/`.
- LiteAPI official documentation and sandbox.
- Commercial agreement or written commercial responses.
- Product, finance, privacy, security and support decisions.

## Work Breakdown

### Milestone 1 — Product and Commercial Boundary

- [ ] Resolve OI-0001: MVP product scope.
- [ ] Resolve OI-0007: launch market, locale and currency.
- [ ] Resolve OI-0002: commercial and merchant-of-record model.
- [ ] Resolve OI-0012: customer support model.
- [x] Review ADR-0001.

### Milestone 2 — Supplier Evidence

- [ ] Validate hotel search, prebook, payment, booking, retrieval and cancellation/refund behaviour in sandbox.
- [ ] Resolve OI-0005: webhook catalogue and guarantees.
- [ ] Resolve OI-0003 and OI-0004 for the flight roadmap without blocking a hotel-first MVP.
- [ ] Update the LiteAPI evidence register with dates and evidence.

### Milestone 3 — Security, Data and Payment

- [ ] Resolve OI-0006: payment integration and PCI scope.
- [ ] Resolve OI-0008: traveller/passport data policy.
- [ ] Resolve OI-0011: payload/evidence retention.
- [x] Review ADR-0003, ADR-0004 and ADR-0007.

### Milestone 4 — Architecture Acceptance

- [x] Review ADR-0002: API/supplier boundary.
- [x] Review ADR-0005: reconciliation/operational status boundary.
- [ ] Resolve OI-0009.
- [x] Review ADR-0006.
- [x] Record accepted outcomes without hiding alternatives.

### Milestone 5 — Implementation Planning Gate

- [ ] Freeze an explicitly versioned MVP scope.
- [ ] Define end-to-end workflows and failure outcomes.
- [ ] Produce the domain/API/test/environment planning inputs.
- [ ] Create the first implementation plan only after all P0 blockers for its scope are resolved.

## Validation

- Every accepted decision has explicit owner approval.
- Every vendor capability used by the MVP has dated documentation, sandbox and commercial evidence as applicable.
- Local documentation links resolve.
- The MVP has no dependency on an unresolved P0 issue.
- Payment, booking and refund mismatches have defined recovery ownership.

## Risks

- Commercial responses may invalidate the preferred payment or hotel-first model.
- Flight documentation may appear complete while account coverage and servicing remain insufficient.
- Product scope may expand faster than decisions are resolved.
- Privacy/security work may reveal that proposed traveller features are unsuitable for the MVP.

## Open Questions

All current questions are indexed in [Open Issues](../../issues/index.md).

## Completion Criteria

- [x] ADR-0001 through ADR-0007 are accepted, rejected or revised with evidence.
- [ ] All P0 issues relevant to the selected MVP are resolved.
- [ ] Product scope, launch market, supplier/payment responsibilities, data policy and support model are explicit.
- [ ] The project can describe every happy path, failure path and external owner needed for the first release.
- [ ] A reviewable implementation plan can be written without inventing unresolved product behaviour.

## Change Log

- 2026-07-28: Recorded acceptance of ADR-0001 through ADR-0007 while retaining unresolved commercial, PCI and supplier evidence as open-issue production gates.
- 2026-07-27: Created the consumer-project planning-readiness plan after replacing unrelated documentation.
