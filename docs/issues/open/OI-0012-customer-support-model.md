---
issue_id: OI-0012
title: Decide the MVP Customer Support Model
status: open
type: product-question
priority: p1
severity: high
created: 2026-07-27
updated: 2026-07-27
decision_owners:
  - Product
related_adrs:
  - ADR-0001
related_plans:
  - PLAN-0001
related_docs:
  - docs/operations/reliability-and-supportability.md
blocked_by:
  - OI-0001
  - OI-0002
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0012 — Decide the MVP Customer Support Model

## Summary

Define how customers obtain help for pending bookings, cancellations, refunds, supplier changes and account problems.

## Context

Travel transactions can require urgent human intervention. The product cannot promise self-service where the supplier or commercial model requires manual handling.

## Options

### Option A — Email/ticket support with defined urgent escalation

Launch with structured support cases, published service hours and a separate path for imminent travel or financial mismatch.

### Option B — Live chat during service hours

Provide synchronous support backed by case history and supplier escalation.

### Option C — Self-service only

Direct customers to automated flows and supplier/carrier contacts.

## Recommendation

Choose Option A for the MVP. It is operable for a small project while still recognising urgent travel cases. Add chat only when volume and staffing justify it; reject self-service-only for financially sensitive bookings.

## Evidence Required

- Supplier support contacts, hours and escalation SLA.
- Expected booking volume and customer timezones.
- Manual servicing cases from OI-0004.
- Ownership for payment/booking mismatch and refunds.
- Privacy and privileged-access controls for support.

## Decision Impact

Controls product promise, staffing, notification copy, support tooling and operational readiness.

## Acceptance Criteria

- [ ] Channels, hours and urgent criteria are defined.
- [ ] Supplier escalation and ownership are documented.
- [ ] Customer-facing service expectations are approved.
- [ ] Support data access and audit requirements are defined.

## Related Documents

- [Reliability and Supportability](../../operations/reliability-and-supportability.md)
