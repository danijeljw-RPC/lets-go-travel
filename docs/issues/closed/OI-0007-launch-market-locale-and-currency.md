---
issue_id: OI-0007
title: Decide Launch Market Locale and Currency
status: closed
type: product-question
priority: p1
severity: medium
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Product
  - Finance
related_adrs:
  - ADR-0001
related_plans:
  - PLAN-0001
related_docs:
  - docs/product/consumer-mvp-scope.md
  - docs/applications/client-strategy.md
blocked_by:
  - OI-0002
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0007 — Decide Launch Market Locale and Currency

## Summary

Define the initial customer market, point of sale, display/transaction currency, language and timezone expectations.

## Context

The initial operating focus, customer eligibility and locale foundation needed an explicit decision.

## Options

### Option A — Australia, English (Australia), AUD

Launch to Australian consumers with `en-AU`, AUD-first display and Australian support/legal assumptions.

### Option B — Australia and New Zealand

Support `en-AU`/`en-NZ`, AUD/NZD and both legal/market configurations from launch.

### Option C — Multi-market from launch

Support several locales, currencies and points of sale immediately.

## Recommendation

Option A was selected with a globally accessible consumer boundary. Australia is the initial operating market with `en-AU` and AUD defaults, but customers from other countries are not geo-blocked.

## Decision

Australia is the initial operating focus. The first UI defaults to `en-AU` and AUD, while customers may select their locale from the anonymous web experience and their authenticated profile. Anonymous locale preference is stored in a secure same-site cookie; authenticated BCP 47 locale preference is stored in PostgreSQL. Version-controlled .NET localisation resources provide UI translations with `en-AU` fallback.

Currency is independent from language. The platform preserves supplier, transaction, charged, settlement and refund currency even when AUD is the initial display default. Additional commercial points of sale and transaction currencies require supplier and legal evidence, but access is not restricted by customer country.

## Evidence Required

- Product confirmation of target customer and support hours.
- Supplier coverage and commercial permission for Australian point of sale.
- AUD payment, settlement and refund behaviour.
- Australian consumer-law, privacy and travel-business advice.

## Decision Impact

Controls search point of sale, pricing display, terms, locale, notifications, support and legal review.

## Acceptance Criteria

- [x] Launch country and eligible customers are explicit.
- [x] Default locale, display currency and timezone/currency handling are explicit.
- [x] Supplier and legal evidence remains a production gate for additional points of sale rather than an unresolved product choice.
- [x] Additional market operations and currencies are deferred while global customer access remains permitted.

## Related Documents

- [Consumer MVP Scope](../../product/consumer-mvp-scope.md)
- [Client Strategy](../../applications/client-strategy.md)
