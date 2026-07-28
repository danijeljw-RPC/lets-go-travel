---
adr_id: ADR-0002
title: Platform API and Private Supplier Boundary
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
related_issues:
  - OI-0002
  - OI-0005
related_adrs:
  - ADR-0001
related_plans:
  - PLAN-0001
related_docs:
  - docs/api/api-principles.md
  - docs/integrations/supplier-integration-principles.md
---

<!-- markdownlint-disable MD013 MD025 -->

# ADR-0002 — Platform API and Private Supplier Boundary

## Status

Accepted on 2026-07-28.

## Context

Web and later mobile clients need a stable product contract while supplier credentials, models and capabilities may change.

## Decision Drivers

- Keep supplier API keys out of public clients.
- Centralise authorisation, pricing, idempotency and booking state.
- Permit supplier replacement without client redesign.
- Keep web and mobile business behaviour consistent.

## Options Considered

### Option A — Platform API as the only application boundary

Clients authenticate with Keycloak and call an ASP.NET Core Web API. Server-side adapters call suppliers and map to platform models.

### Option B — Clients call suppliers directly

Clients use supplier endpoints or SDKs for search, payment and booking.

### Option C — Hybrid supplier access

Clients call the platform for customer data but call suppliers directly for selected search or booking operations.

## Recommendation

Choose Option A. It protects credentials and makes platform ownership, compatibility, recovery and supplier substitution explicit.

## Decision

Option A is accepted. Application clients authenticate with Keycloak and call the platform API as their only application boundary. Server-side adapters hold supplier credentials and map supplier interactions to platform models.

## Consequences

### Positive

- One security and business-rule boundary.
- Supplier-neutral clients and platform identifiers.
- Better correlation, recovery and audit.

### Negative

- The platform operates and scales all client/supplier traffic.
- Supplier mapping and error translation become platform responsibilities.

### Risks

Leaky abstractions may still expose supplier concepts unless API and domain reviews enforce the boundary.

## Dependencies

Commercial supplier access and exact webhook/payment flows remain open.

## Related Documents

- [API Principles](../../api/api-principles.md)
- [Supplier Integration Principles](../../integrations/supplier-integration-principles.md)
