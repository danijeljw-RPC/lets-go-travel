---
plan_id: PLAN-0001
title: Project Planning Readiness
status: active
owner: Product and Architecture
created: 2026-07-27
updated: 2026-07-29
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
  - ADR-0008
  - ADR-0009
  - ADR-0010
related_docs:
  - docs/product/consumer-mvp-scope.md
  - docs/integrations/liteapi-nuitee-connect.md
  - docs/architecture/target-architecture.md
  - docs/security/data-retention-and-legal-hold.md
  - docs/decisions/review-register.md
scope:
  - docs
  - product-discovery
  - supplier-validation
---

<!-- markdownlint-disable MD013 MD025 -->

# PLAN-0001 — Project Planning Readiness

## Status

Implementation-ready. Product, architecture, operational and technical direction is sufficient for MVP code. This plan remains active only as the production supplier/legal evidence tracker; those gates do not block supplier-neutral implementation or sandbox work. OI-0010 is a non-blocking post-MVP wishlist item.

## Purpose

Turn the consumer travel foundation into an evidence-backed set of product and architecture decisions that is specific enough to produce the first implementation plan safely.

## Background

The repository contains a compact consumer-project documentation system. Ten ADRs are accepted. Seven issues are closed, four selected directions are in review for production evidence and one open issue is an explicitly non-blocking post-MVP wishlist item.

## Scope

### In Scope

- Maintain the accepted hotel, flight and combined hotel-plus-flight product scope and Australia-first global-access boundary.
- Obtain LiteAPI commercial, hotel, flight, servicing, webhook and payment evidence.
- Carry the accepted sensitive-traveller opt-in, Blazor SSR and ticket-support policies into implementation-ready inputs.
- Maintain the accepted ADR baseline as in-review supplier/legal evidence is resolved.
- Produce an implementation-ready MVP scope and dependency map.

### Out of Scope

- Application implementation under PLAN-0002; this plan tracks discovery and production evidence only.
- Database schema, endpoint or infrastructure design beyond decision-level architecture.
- Production supplier onboarding.
- Native mobile delivery.
- Live operational flight-status integration unless separately approved.

## Inputs

- Structured documentation under `docs/`.
- LiteAPI official documentation and sandbox.
- Commercial agreement or written commercial responses.
- Product, finance, privacy, security and support decisions.

## Work Breakdown

### Milestone 1 — Product and Commercial Boundary

- [x] Resolve OI-0001: hotel, flight and combined hotel-plus-flight journeys are initial scope.
- [x] Resolve OI-0007: Australia-first global access, `en-AU`/AUD defaults and selectable locale.
- [ ] Complete OI-0002 review: obtain written commercial and merchant-of-record evidence for the selected LiteAPI route.
- [x] Resolve OI-0012: first-party ticket support with email updates, guest magic links and private attachments.
- [x] Defer manual itinerary items, sharing, loyalty, native offline access, push and SMS; initial notifications use email and in-app records.
- [x] Review ADR-0001.

### Milestone 2 — Supplier Evidence

- [ ] Validate account access plus hotel search/content, rates, occupancy, prebook, payment, booking, retrieval, cancellation/refund and content-licensing behaviour in sandbox and commercial evidence.
- [ ] Validate request/look-to-book limits, data-retention permissions, markup/commission rules and settlement behaviour.
- [ ] Validate taxes/fees, pay-at-property and multi-room/child-occupancy behaviour plus hotel booking-change and relocation handling.
- [ ] Complete OI-0005 review: obtain the webhook catalogue and delivery guarantees for the selected inbox-plus-reconciliation model.
- [x] Close OI-0004 with a partly manual, capability-gated servicing boundary; retain carrier lifecycle evidence under OI-0003 before production enablement.
- [x] Add dated LiteAPI evidence reviews for OI-0002, OI-0003, OI-0005 and OI-0006.

### Milestone 3 — Security, Data and Payment

- [ ] Complete OI-0006 review: prove the selected hosted/SDK integration and qualified PCI scope.
- [x] Resolve OI-0008: reusable sensitive traveller data requires granular opt-in and is off by default.
- [x] Define the OI-0011 project schedule, raw-payload allowlist, 35-day backup expiry and matter-specific legal-hold procedure.
- [x] Close OI-0011 as a data-governance decision; retain executed-contract, Australian approval and control verification as production gates.
- [x] Define the Australian privacy, consumer-law/pricing, travel-selling/licensing, insolvency/trust, cross-border/data-residency, minor-traveller and breach-response implementation baseline.
- [x] Define the required terms/privacy notices, consent and supplier-term clause schedule; final documents remain a production approval.
- [x] Review ADR-0003, ADR-0004 and ADR-0007.

### Milestone 4 — Architecture Acceptance

- [x] Review ADR-0002: API/supplier boundary.
- [x] Review ADR-0005: reconciliation/operational status boundary.
- [x] Resolve OI-0009: .NET 10 Blazor SSR with ASP.NET Core Web API.
- [x] Review ADR-0006.
- [x] Accept ADR-0008: dedicated durable flight-reconciliation worker and customer notification schedule.
- [x] Defer OI-0010 live operational flight status as a non-blocking post-MVP wishlist item.
- [x] Record accepted outcomes without hiding alternatives.

### Milestone 5 — Implementation Planning Gate

- [x] Freeze the explicitly versioned MVP scope in the MVP delivery design.
- [x] Define the implementation slices and failure boundaries for registration/login, hotel booking, flight booking, combined journey, price change, payment challenge/mismatch, pending booking, webhook, reconciliation, schedule change, cancellation, refund, support escalation and account deletion.
- [x] Produce the domain/API inputs for identifiers, booking states, supplier capabilities, money/time, `/api/v1`, reconciliation, canonical versions and external-client compatibility.
- [x] Select durable PostgreSQL work, no initial distributed cache, S3-compatible private storage, ClamAV, Azure Key Vault, OpenTelemetry, protected support administration and GitHub Actions.
- [ ] Define anonymous-cookie and authenticated-profile locale resolution, .NET localisation resource fallback and localised notification-template validation.
- [x] Define customer response to a closed ticket: reopen the same thread as `Waiting on Support` and preserve the closure event.
- [x] Define unit, contract, architecture, integration, sandbox and end-to-end test boundaries plus local/test/sandbox/staging/production separation.
- [x] Define CI/CD, observability, sandbox certification, production-readiness and launch planning inputs.
- [x] Create the first implementation plan for the unblocked application-foundation slice.

## Validation

- Every accepted decision has explicit owner approval.
- Every vendor capability used by the MVP has dated documentation, sandbox and commercial evidence as applicable.
- Local documentation links resolve.
- The MVP has no dependency on an unresolved P0 issue.
- Payment, booking and refund mismatches have defined recovery ownership.

## Risks

- Commercial responses may block the selected LiteAPI payment or flight activation route and require capability suppression or a later provider decision.
- Flight documentation may appear complete while account coverage and servicing remain insufficient.
- Product scope may expand faster than decisions are resolved.
- Privacy/security work may reveal that proposed traveller features are unsuitable for the MVP.

## Remaining Reviews

The [Remaining Review Register](../../decisions/review-register.md) is the consolidated report for production activation, operational and technical reviews. The [Issue Index](../../issues/index.md) remains authoritative for issue lifecycle.

## Completion Criteria

- [x] ADR-0001 through ADR-0008 are accepted, rejected or revised with evidence.
- [x] All P0 design issues relevant to starting MVP code are resolved or isolated as disabled production-activation gates.
- [x] Product scope, launch market, data policy and support model are explicit.
- [ ] Supplier/payment responsibilities are evidenced for production.
- [x] The project can describe every happy path, failure category and external owner needed for incremental MVP delivery; provider-specific production results remain evidence gates.
- [x] A reviewable first implementation plan is written without inventing unresolved product behaviour.

## Change Log

- 2026-07-29: Reconciled the supplied LiteAPI and Australian legal evidence; closed OI-0004/OI-0011 as design decisions; accepted ADR-0009/ADR-0010; fixed operational defaults; approved MVP code start; and created the MVP delivery design and foundation plan.
- 2026-07-28: Completed the repository-wide review, approved the exact retention/legal-hold baseline, deferred non-transactional product extras, selected ticket reopening behaviour and added the consolidated remaining-review register.
- 2026-07-28: Selected OI-0011 Option A and moved it to in review; distinguished seven-year corporate financial records, general five-year tax records, privacy deletion and matter-specific legal holds from raw supplier payload retention.
- 2026-07-28: Recorded OI-0010 as an explicitly non-blocking post-MVP wishlist item; launch uses reconciled itinerary data and directs customers to the airline for live operational status.
- 2026-07-28: Closed OI-0001, OI-0007, OI-0008, OI-0009 and OI-0012; moved OI-0002 through OI-0006 to in review; retained OI-0010/OI-0011 as open deferrals; accepted ADR-0008 and synchronised the product, locale, traveller, payment, reconciliation and ticket-support direction.
- 2026-07-28: Consolidated the root discovery pack into canonical documents under `docs/` and removed the duplicate source files.
- 2026-07-28: Recorded acceptance of ADR-0001 through ADR-0007 while retaining unresolved commercial, PCI and supplier evidence as open-issue production gates.
- 2026-07-27: Created the consumer-project planning-readiness plan after replacing unrelated documentation.
