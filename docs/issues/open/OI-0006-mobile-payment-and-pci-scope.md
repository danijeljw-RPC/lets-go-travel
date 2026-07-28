---
issue_id: OI-0006
title: Select Web and Mobile Payment Integration and Confirm PCI Scope
status: in-review
type: compliance-question
priority: p0
severity: critical
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Finance
  - Security
  - Compliance
related_adrs:
  - ADR-0007
related_plans:
  - PLAN-0001
related_docs:
  - docs/security/payment-and-pci-scope.md
  - docs/applications/client-strategy.md
blocked_by:
  - OI-0002
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0006 — Select Web and Mobile Payment Integration and Confirm PCI Scope

## Summary

Select supported web and mobile payment flows and obtain an evidence-backed PCI DSS scope assessment.

## Context

LiteAPI documents a JavaScript payment SDK and Stripe-related flows. That does not prove suitability for native mobile, the final merchant arrangement, Australian payment methods or the applicable SAQ.

## Options

### Option A — Full hosted redirect

Use system browser/provider-hosted checkout with verified return and deep-link handling.

### Option B — Approved embedded provider component

Use official hosted fields, Elements or SDK components that send card data directly to the provider.

### Option C — Web view or custom card form

Embed the web flow in a mobile web view or send card data through platform-controlled UI/backend.

## Recommendation

Prefer Option A where supported. Use Option B only after vendor and PCI review. Do not use Option C for the MVP unless a qualified review explicitly establishes support and scope.

## Selected Direction

The product owner selected the officially supported LiteAPI SDK or hosted component route on 2026-07-28. For the .NET 10 Blazor web client, use LiteAPI's approved hosted JavaScript or hosted payment experience where supported, with raw card data sent directly to the responsible provider. A later native client uses an officially supported platform SDK or system-browser hosted flow where available.

This is Option B where LiteAPI provides an approved embedded component, with Option A as the safe fallback where only hosted redirection is supported. Custom card forms and unsupported embedded web views remain prohibited. The issue is in review until platform support and qualified PCI scope are evidenced.

## Evidence Reviewed

- Product-owner SDK/hosted-component direction recorded on 2026-07-28.
- Public LiteAPI payment documentation already referenced by ADR-0007.
- No qualified PCI assessment, supported-platform matrix or production component approval has been recorded.

## Evidence Required

- Vendor-supported browser and Android/iOS integration patterns.
- Return URL, deep link, abandonment and duplicate callback behaviour.
- Supported Australian payment methods and 3-D Secure handling.
- Merchant-of-record responsibility from OI-0002.
- Provider Attestation of Compliance and qualified PCI scope/SAQ advice.
- Script security and vulnerability-management obligations.

## Decision Impact

Blocks checkout architecture, mobile planning, security controls and production readiness.

## Acceptance Criteria

- [ ] Web flow is selected and sandbox-proven.
- [ ] Future mobile flow is either selected or explicitly deferred.
- [ ] PCI scope and responsibilities are recorded by a qualified reviewer/provider.
- [ ] Each selected production route satisfies ADR-0007 and is approved for activation.

## Related Documents

- [Payment and PCI Scope](../../security/payment-and-pci-scope.md)
- [ADR-0007](../../adr/accepted/ADR-0007-payment-data-and-pci-scope-minimisation.md)
