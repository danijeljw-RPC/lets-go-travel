---
issue_id: OI-0008
title: Decide Saved Traveller and Passport Data Policy
status: closed
type: privacy-question
priority: p1
severity: high
created: 2026-07-27
updated: 2026-07-28
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

<!-- markdownlint-disable MD013 MD025 -->

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

The product owner selected a constrained form of Option B on 2026-07-28: sensitive traveller data may be saved only after a separate, explicit, granular customer opt-in that is off by default.

## Decision

Customers enter date of birth, passport and identity-document data for each booking by default. The platform does not create a reusable sensitive traveller profile from booking submission, account registration, general privacy acceptance or a low-risk saved-traveller record.

Saving date of birth or identity documents for future bookings requires an unchecked, affirmative control that names the saved categories and purpose. Consent is recorded separately and the customer can remove future-use storage without rewriting evidence required for an existing booking. Activation requires field-level protection, masking, least-privilege access, audit, key management, retention and deletion controls.

Booking-time snapshots and reusable profile storage are separate data purposes. Closed OI-0011 records the selected [retention baseline](../../security/data-retention-and-legal-hold.md) and its production validation gates.

## Evidence Required

- Supplier-required fields by product/market.
- Privacy impact assessment and legal retention advice.
- Encryption/key-management, masking, audit and support-access design for any sensitive saved fields.
- Account deletion and active-booking policy.
- Mobile offline-data policy.

## Decision Impact

Controls data model, booking flow, privacy notices, security design, support access and deletion.

## Acceptance Criteria

- [x] Allowed sensitive saved fields require category-specific opt-in.
- [x] Passport/identity-document storage is off by default and explicitly optional.
- [x] Protection, access, audit and deletion are activation gates; booking-evidence retention is defined by closed OI-0011.
- [x] Customer consent is separate, granular, affirmative and revocable for future-use storage.

## Related Documents

- [Security, Privacy and Data Ownership](../../security/security-privacy-and-data-ownership.md)
- [Trips, Travellers and Bookings](../../domain/trips-travellers-and-bookings.md)
