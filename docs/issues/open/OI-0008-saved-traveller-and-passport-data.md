---
issue_id: OI-0008
title: Decide Saved Traveller and Passport Data Policy
status: open
type: privacy-question
priority: p1
severity: high
created: 2026-07-27
updated: 2026-07-27
decision_owners:
  - Product
  - Privacy
  - Security
related_adrs:
  - ADR-0003
related_plans:
  - PLAN-0001
related_docs:
  - docs/security/security-privacy-and-data-ownership.md
  - docs/domain/trips-travellers-and-bookings.md
blocked_by: []
---

# OI-0008 — Decide Saved Traveller and Passport Data Policy

## Summary

Decide which traveller details may be saved outside an active booking and whether passport/identity-document data is retained.

## Context

Saved travellers improve repeat booking, but passport, date-of-birth and loyalty data increase privacy, breach, support and deletion obligations.

## Options

### Option A — Minimal saved traveller, no stored passport in MVP

Save name, relationship label and low-risk preferences. Collect required document data during booking and retain only where booking evidence/legal obligations require it.

### Option B — Encrypted saved passport profile

Allow opt-in storage with field-level encryption, masking, strict access, retention and deletion.

### Option C — No saved travellers

Collect all traveller details per booking.

## Recommendation

Choose Option A. It preserves useful repeat-traveller capability while avoiding a passport vault before the product demonstrates need and completes a privacy/security design.

## Evidence Required

- Supplier-required fields by product/market.
- Privacy impact assessment and legal retention advice.
- Encryption/key-management, masking, audit and support-access design for any sensitive saved fields.
- Account deletion and active-booking policy.
- Mobile offline-data policy.

## Decision Impact

Controls data model, booking flow, privacy notices, security design, support access and deletion.

## Acceptance Criteria

- [ ] Allowed saved fields are enumerated.
- [ ] Passport/identity-document decision is explicit.
- [ ] Retention, access, encryption and deletion rules are approved.
- [ ] Customer consent and disclosure requirements are documented.

## Related Documents

- [Security, Privacy and Data Ownership](../../security/security-privacy-and-data-ownership.md)
- [Trips, Travellers and Bookings](../../domain/trips-travellers-and-bookings.md)
