---
issue_id: OI-0012
title: Decide the MVP Customer Support Model
status: closed
type: product-question
priority: p1
severity: high
created: 2026-07-27
updated: 2026-07-28
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

Option A was selected on 2026-07-28. The initial product uses first-party asynchronous ticket support and email updates, with no built-in live chat.

## Decision

Authenticated customers and guests can create support tickets with name, email, optional booking/customer reference, required category, message and permitted attachments. An authenticated customer's email is populated from the account authority and cannot be edited in the form.

Every ticket starts as `New`. An accepted customer reply makes the ticket `Waiting on Support`; an accepted support response makes it `Waiting on Customer`; an authorised support user can set `Closed`. Whether a later customer reply reopens a closed ticket or creates a linked follow-up ticket must be selected and tested during implementation planning.

Each ticket uses an internal UUIDv7 identifier plus a separate cryptographically random bearer token for guest access. Only the token hash is stored. UUIDv7 is not the access secret. The emailed magic link can be revoked or rotated and grants access only to its ticket.

Attachments are private objects in an S3-compatible store and become downloadable only after ticket authorisation through short-lived signed access or an application stream. Every accepted thread update persists first and queues a durable, retryable, deduplicated email notification. A later third-party chat adapter may create or append ticket messages without replacing the support module's authority or ticket history.

## Evidence Required

- Supplier support contacts, hours and escalation SLA.
- Expected booking volume and customer timezones.
- Manual servicing cases from OI-0004.
- Ownership for payment/booking mismatch and refunds.
- Privacy and privileged-access controls for support.

## Decision Impact

Controls product promise, staffing, notification copy, support tooling and operational readiness.

## Acceptance Criteria

- [x] Ticket and email channels are selected; service hours and urgent criteria remain operational configuration before launch.
- [x] Supplier escalation evidence remains part of OI-0002/OI-0004 production readiness.
- [x] The asynchronous support expectation and state model are approved.
- [x] Guest access, private attachment, immutable thread and notification boundaries are defined.

## Related Documents

- [Reliability and Supportability](../../operations/reliability-and-supportability.md)
