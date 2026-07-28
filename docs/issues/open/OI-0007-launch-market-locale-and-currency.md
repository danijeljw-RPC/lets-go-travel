---
issue_id: OI-0007
title: Decide Launch Market Locale and Currency
status: open
type: product-question
priority: p1
severity: medium
created: 2026-07-27
updated: 2026-07-27
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

# OI-0007 — Decide Launch Market Locale and Currency

## Summary

Define the initial customer market, point of sale, display/transaction currency, language and timezone expectations.

## Context

The source questions focus on Australian coverage and law, but the launch configuration has not been explicitly decided.

## Options

### Option A — Australia, English (Australia), AUD

Launch to Australian consumers with `en-AU`, AUD-first display and Australian support/legal assumptions.

### Option B — Australia and New Zealand

Support `en-AU`/`en-NZ`, AUD/NZD and both legal/market configurations from launch.

### Option C — Multi-market from launch

Support several locales, currencies and points of sale immediately.

## Recommendation

Choose Option A for the MVP. Preserve correct currency/timezone models so later markets do not require data migration, but do not build multi-market operations before product evidence requires them.

## Evidence Required

- Product confirmation of target customer and support hours.
- Supplier coverage and commercial permission for Australian point of sale.
- AUD payment, settlement and refund behaviour.
- Australian consumer-law, privacy and travel-business advice.

## Decision Impact

Controls search point of sale, pricing display, terms, locale, notifications, support and legal review.

## Acceptance Criteria

- [ ] Launch country and eligible customers are explicit.
- [ ] Default locale, transaction/display currencies and timezone handling are explicit.
- [ ] Supplier and legal evidence supports the choice.
- [ ] Deferred markets are listed.

## Related Documents

- [Consumer MVP Scope](../../product/consumer-mvp-scope.md)
- [Client Strategy](../../applications/client-strategy.md)
