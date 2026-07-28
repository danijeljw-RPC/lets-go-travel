---
issue_id: OI-0011
title: Decide Supplier Payload and Booking Evidence Retention
status: in-review
type: privacy-question
priority: p1
severity: high
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Data
  - Privacy
  - Compliance
related_adrs:
  - ADR-0004
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
blocked_by:
  - OI-0002
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0011 — Decide Supplier Payload and Booking Evidence Retention

## Summary

Define what raw supplier payloads, canonical booking versions and financial/support evidence may be retained and for how long.

## Context

Raw payloads can help disputes and mapping diagnosis but may contain unnecessary personal data, licensed content and volatile fields. Canonical versions may have different retention needs.

## Options

### Option A — Canonical history with selective protected raw evidence

Retain canonical versions according to booking/legal policy and retain raw payloads only for selected operations and shorter periods where contract permits.

### Option B — Retain all raw payloads indefinitely

Store every supplier request/response as permanent evidence.

### Option C — Retain no raw payloads

Keep only normalised state, versions and supplier references.

## Recommendation

Choose Option A. It balances explainability with minimisation and supplier licensing, provided access, encryption and deletion are explicit.

## Selected Direction

The product owner selected Option A on 2026-07-28. The platform retains supplier-neutral canonical booking versions and the minimum financial/support evidence required for the customer, dispute resolution and applicable law. Complete LiteAPI payloads are retained only for allowlisted troubleshooting, ambiguous-operation, incident or dispute purposes, for a shorter configured period where LiteAPI terms permit.

No seven-year blanket applies to every booking field or supplier payload. Section 286 of the Australian Corporations Act requires qualifying company financial records to be retained for seven years after the covered transactions are completed. Australian tax records are generally retained for five years, subject to longer special cases. Personal information that is no longer needed must be destroyed or de-identified unless an Australian law or court/tribunal order requires retention.

A legal hold suspends the ordinary deletion schedule only for records relevant to an actual or reasonably anticipated dispute, litigation, investigation, audit, subpoena or regulator/court requirement. It is matter-specific and remains until an authorised legal/compliance owner releases it; it is not a default seven-year retention category.

This issue is in review until the LiteAPI agreement, record classification, exact periods, backup expiry and hold/release procedure are approved.

## Evidence Reviewed

- Product-owner selection of Option A on 2026-07-28.
- [Corporations Act 2001, section 286](https://www.legislation.gov.au/C2004A00818/latest/text): qualifying company financial records are retained for seven years after the relevant transactions are completed.
- [ATO Taxation Ruling TR 96/7](https://www.ato.gov.au/law/view/document?LocID=%22TXR%2FTR967%2FNAT%2FATO%2F00001%22): business tax records are generally retained for five years, with specific exceptions.
- [OAIC APP 11 guidance](https://www.oaic.gov.au/privacy/australian-privacy-principles/australian-privacy-principles-guidelines/chapter-11-app-11-security-of-personal-information): personal information no longer needed must be destroyed or de-identified unless legal or court/tribunal retention applies, including reasonable treatment of archived and backup copies.
- LiteAPI contractual retention and licensing terms have not yet been recorded.

## Evidence Required

- Supplier contractual retention/licensing terms.
- Legal, dispute, refund and financial recordkeeping requirements.
- Field-level payload classification.
- Storage, backup and deletion design.
- Support and incident-investigation need.

## Decision Impact

Controls data storage, privacy risk, support evidence, canonicalisation testing and deletion workflows.

## Acceptance Criteria

- [ ] Retention schedule exists by record type.
- [ ] Raw-payload allowlist and protection are defined.
- [ ] Backup/deletion behaviour is defined.
- [ ] ADR-0004 and privacy docs are updated.

## Related Documents

- [Booking Reconciliation and Version History](../../domain/booking-reconciliation-and-version-history.md)
