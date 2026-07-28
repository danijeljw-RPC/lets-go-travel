---
issue_id: OI-0011
title: Decide Supplier Payload and Booking Evidence Retention
status: open
type: privacy-question
priority: p1
severity: high
created: 2026-07-27
updated: 2026-07-27
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
