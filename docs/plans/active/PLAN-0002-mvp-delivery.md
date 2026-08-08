---
plan_id: PLAN-0002
title: MVP Delivery
status: active
owner: Product and Engineering
created: 2026-07-29
updated: 2026-08-08
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
  - docs/superpowers/specs/2026-07-29-mvp-delivery-design.md
  - docs/superpowers/plans/2026-07-29-mvp-foundation.md
  - docs/decisions/review-register.md
scope:
  - application
  - tests
  - containers
  - delivery
---

<!-- markdownlint-disable MD013 MD025 -->

# PLAN-0002 — MVP Delivery

## Status

Active. Slices 1 through 5 are implemented and verified. Slice 6 first-party support is next. Production supplier, payment, webhook and notification capabilities remain disabled until their review-register gates are approved.

## Purpose

Deliver the consumer MVP through independently testable vertical slices while preserving the accepted supplier-neutral, consumer-only and compliance-first boundaries.

## Slice Sequence

- [x] Slice 1: .NET 10 solution, API contract, Blazor SSR, workers, containers and CI.
- [x] Slice 2: Keycloak identity, locale, customers, trips and travellers.
- [x] Slice 3: supplier-neutral hotel/flight search, pricing and capability registry.
- [x] Slice 4: checkout, LiteAPI hosted payment, hotel/flight booking and combined journeys.
- [x] Slice 5: webhooks, reconciliation, immutable versions and notifications.
- [ ] Slice 6: first-party support tickets, guest magic links and private attachments.
- [ ] Slice 7: retention/privacy operations and production-readiness certification.

## Delivery Rules

- Each slice has a reviewed specification or accepted ADR input, a detailed implementation plan, TDD, documentation updates, fresh verification and its own commit history.
- Every production supplier/payment capability defaults off and requires its recorded activation evidence.
- No slice introduces native mobile, live operational flight status, insurance, stored value, credit, sharing, loyalty, AI planning or other deferred scope.
- Provider payloads never become the platform public contract.
- A combined journey keeps component payment, booking, confirmation, cancellation and refund outcomes explicit.

## Current Plan

Slice 5 followed the completed [Booking Reconciliation Implementation Plan](../../superpowers/plans/2026-08-08-slice-5-booking-reconciliation.md). Its [outcome report](../../delivery/2026-08-08-slice-5-booking-reconciliation-outcome.md) records scope, verification and unchanged production gates. The next implementation plan must cover Slice 6 only: first-party support tickets, guest magic links and private attachments.

## Change Log

- 2026-07-29: Completed Slice 1 with the .NET 10 solution, public API contract, Blazor SSR host, cooperative workers, non-root containers, locked CI and automated documentation validation.
- 2026-07-29: Completed Slice 2 with subject-owned consumer profiles, locale handling, trips, low-risk travellers, PostgreSQL migrations, authenticated API routes, Blazor SSR account pages and a pinned local Keycloak/PostgreSQL runtime.
- 2026-07-29: Completed Slice 3 with supplier-neutral hotel/flight contracts, minimum-total pricing, sanitized LiteAPI fixtures, an environment/market/operation/carrier capability registry, public API routes and a Blazor SSR search shell. Production search remains disabled.
- 2026-07-29: Completed Slice 4 with server-resolved checkout, renewed price acceptance, hosted-payment isolation, separately evidenced hotel/flight component bookings, durable idempotency, immediate recovery, PostgreSQL migration, authenticated API routes and a Blazor checkout experience. Production payment and booking remain disabled.
- 2026-08-08: Completed Slice 5 with authenticated fail-closed webhook ingress, durable inbox and reconciliation schedules, retrieval-driven immutable canonical booking versions, customer-safe history, deduplicated notification intents and private worker processing. Production webhook, supplier and email capabilities remain disabled.

## Production Gates

The [Remaining Review Register](../../decisions/review-register.md) is authoritative. An incomplete production gate does not block ordinary implementation unless the code would otherwise invent the missing commercial/legal fact or expose the capability to customers.

## Completion Criteria

- Hotel, flight and combined journeys operate end to end under the approved supplier capabilities.
- Payment and booking mismatches recover without duplicate charge/booking.
- Webhook and scheduled reconciliation converge to explainable booking history.
- Customer and guest support flows are secure and auditable.
- Locale, pricing, minor-traveller, privacy and retention controls meet the documented implementation baseline.
- Every enabled production capability has completed evidence and approval.
