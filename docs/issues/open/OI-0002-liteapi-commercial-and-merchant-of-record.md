---
issue_id: OI-0002
title: Confirm LiteAPI Commercial and Merchant-of-Record Model
status: in-review
type: integration-question
priority: p0
severity: critical
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Finance
  - Product
  - Compliance
related_adrs:
  - ADR-0007
related_plans:
  - PLAN-0001
related_docs:
  - docs/integrations/liteapi-nuitee-connect.md
  - docs/product/payments-and-pricing.md
blocked_by: []
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0002 — Confirm LiteAPI Commercial and Merchant-of-Record Model

## Summary

Obtain written commercial terms defining merchant of record, settlement, markup, commission, refunds, chargebacks, tax and consumer responsibility.

## Context

Vendor documentation describes user payment and revenue models, but the actual agreement determines which model applies to this project. Technical payment routing does not by itself identify the merchant of record.

## Options

### Option A — Supplier or supplier payment entity is merchant of record

The supplier/payment arrangement owns the customer charge and defined downstream responsibilities, while the platform earns agreed commission or margin.

### Option B — `readytogo.travel` is merchant of record

The platform owns acquiring, customer charge, disputes, refunds, tax and related obligations.

### Option C — Model varies by product or payment method

Hotels, flights or payment methods use different merchant and settlement models.

## Recommendation

Prefer Option A for the initial small product if commercially available and legally clear. It reduces operational burden, but the exact responsibilities must be confirmed in writing before the LiteAPI route can be activated in production under ADR-0007.

## Selected Direction

The product owner selected Option A on 2026-07-28. LiteAPI or its payment entity is the preferred customer-payment and merchant route for the initial product. `readytogo.travel` facilitates the customer journey, initiates the information and operations required by LiteAPI, reconciles booking/payment outcomes and exposes review through its dashboard.

This direction does not establish the legal merchant of record from a technical SDK. The issue is in review until written LiteAPI terms establish the responsible merchant and every item under Evidence Required for hotels, flights and any combined journey.

## Evidence Reviewed

- Product-owner direction recorded on 2026-07-28.
- LiteAPI payment and revenue documentation already referenced by ADR-0007.
- No executed agreement or formal written allocation of merchant, settlement, refund, dispute, tax or consumer responsibilities has been recorded in the repository.

## Evidence Required

- Executed agreement or formal written offer.
- Statement descriptor and customer terms.
- Chargeback, fraud, refund and booking-failure responsibility.
- Net/commissionable rate, markup/parity and settlement rules.
- Tax invoice, GST and consumer-law responsibilities.
- Supplier data retention and payload licensing terms.

## Decision Impact

Blocks production activation of the LiteAPI payment route, final pricing, checkout, terms, accounting and refund operations. It does not block the provider-neutral architecture accepted by ADR-0007.

## Acceptance Criteria

- [ ] Merchant of record is explicit for every MVP product/payment method.
- [ ] Settlement and margin rules are documented.
- [ ] Chargeback/refund responsibilities are documented.
- [ ] Legal/compliance review is recorded.
- [ ] The selected LiteAPI route can be enabled under ADR-0007 without inventing commercial responsibilities.

## Related Documents

- [LiteAPI/Nuitee Connect](../../integrations/liteapi-nuitee-connect.md)
- [Payments and Pricing](../../product/payments-and-pricing.md)
