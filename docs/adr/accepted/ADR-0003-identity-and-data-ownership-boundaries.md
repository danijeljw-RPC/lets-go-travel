---
adr_id: ADR-0003
title: Identity and Data Ownership Boundaries
status: accepted
date_proposed: 2026-07-27
date_accepted: 2026-07-28
date_rejected: null
date_superseded: null
superseded_by: null
supersedes: []
decision_owners:
  - Architecture
  - Security
  - Privacy
related_issues:
  - OI-0008
related_adrs:
  - ADR-0001
  - ADR-0002
related_plans:
  - PLAN-0001
related_docs:
  - docs/architecture/trust-and-data-boundaries.md
  - docs/security/identity-and-access.md
---

<!-- markdownlint-disable MD013 MD025 -->

# ADR-0003 — Identity and Data Ownership Boundaries

## Status

Accepted on 2026-07-28.

## Context

Authentication, product data and supplier fulfilment have different ownership and security requirements.

## Decision Drivers

- Avoid duplicating authentication secrets.
- Keep a durable product-owned customer identity and history.
- Minimise data disclosed to suppliers.
- Support account lifecycle and privacy rights.

## Options Considered

### Option A — Keycloak identity, PostgreSQL product data, supplier fulfilment data

Keycloak owns authentication. PostgreSQL owns customer profile, trips, travellers and platform bookings. Suppliers receive operation-specific fulfilment data.

### Option B — Store most profile data in Keycloak

Use Keycloak attributes as the main customer and traveller record.

### Option C — Supplier-centric customer record

Use supplier user/booking records as the primary customer relationship.

## Recommendation

Choose Option A. It keeps security and product ownership clear while limiting supplier dependency.

## Decision

Option A is accepted. Keycloak owns authentication, PostgreSQL owns product and customer data, and suppliers receive only the fulfilment data required for each operation.

## Consequences

### Positive

- Credentials remain isolated from domain data.
- Supplier replacement does not replace customer accounts or trips.
- Privacy and retention policies can differ by data class.

### Negative

- Overlapping fields such as email, name, phone and locale require an authority/synchronisation rule.
- Account deletion must coordinate identity removal with retained booking evidence.

### Risks

Saving traveller identity documents can materially increase breach impact; closed [OI-0008](../../issues/closed/OI-0008-saved-traveller-and-passport-data.md) requires granular opt-in and protection before reusable storage is activated.

## Dependencies

Privacy/legal review and the protection gates recorded by closed OI-0008.

## Related Documents

- [Trust and Data Boundaries](../../architecture/trust-and-data-boundaries.md)
- [Identity and Access](../../security/identity-and-access.md)
