---
adr_id: ADR-0009
title: MVP Application Contract and Module Foundation
status: accepted
date_proposed: 2026-07-29
date_accepted: 2026-07-29
date_rejected: null
date_superseded: null
superseded_by: null
supersedes: []
decision_owners:
  - Architecture
  - Engineering
related_issues:
  - OI-0001
  - OI-0009
related_adrs:
  - ADR-0001
  - ADR-0002
  - ADR-0006
related_plans:
  - PLAN-0001
related_docs:
  - docs/api/api-principles.md
  - docs/architecture/target-architecture.md
---

<!-- markdownlint-disable MD013 MD025 -->

# ADR-0009 — MVP Application Contract and Module Foundation

## Status

Accepted on 2026-07-29.

## Context

The accepted product and supplier-neutral architecture are detailed enough to begin implementation, but the repository still needs exact solution, module, API compatibility and error-contract conventions.

## Decision Drivers

- One .NET 10 codebase with clear module ownership.
- Blazor SSR and later native clients using the same HTTP contract.
- Provider-neutral contracts that can be tested without production LiteAPI activation.
- Small, reversible foundations without premature distributed systems.
- Consistent errors, traceability, ownership and idempotency.

## Options Considered

### Option A — Feature-oriented modular monolith with explicit API contracts

Use one solution containing the public API, Blazor web application, general worker and dedicated flight-reconciliation worker. Domain code is grouped by product module and exposes only public module interfaces and HTTP contracts.

### Option B — Layer-only monolith

Group all code into shared domain, application and infrastructure layers without feature ownership.

### Option C — Microservices from launch

Deploy each product capability independently with network contracts and separate persistence.

## Decision

Choose Option A.

The solution uses the `ReadyToGoTravel` root namespace and these deployable projects:

- `ReadyToGoTravel.Api` for the public ASP.NET Core Web API;
- `ReadyToGoTravel.Web` for the .NET 10 Blazor Web App using static SSR by default and interactive server rendering only where required;
- `ReadyToGoTravel.Worker` for outbox, inbox, notification, retention and shared background work; and
- `ReadyToGoTravel.FlightReconciliation.Worker` for the dedicated durable flight schedule.

Reusable code is split into small building-block, contract and feature-module projects. A module owns its tables, persistence mappings, commands, queries, validation and public interfaces. Another module cannot query its private tables directly.

Public API routes use an explicit major path beginning with `/api/v1`. Within a major version, changes are additive. Once an external mobile client exists, a superseded major version receives at least 12 months of security and compatibility support from the successor's general availability date unless a critical security or legal issue requires earlier withdrawal. The web client follows the same API rather than using privileged server-side domain access.

ASP.NET Core `ProblemDetails` is the error envelope. Extensions include stable `code`, `correlationId`, optional field errors and retry guidance; raw supplier errors are never returned. Every request accepts or creates a correlation ID and propagates it through persistence, supplier calls, background work and notifications.

The API uses built-in ASP.NET Core OpenAPI generation in development and CI. Production does not expose interactive API documentation by default. Search and public ticket entry receive endpoint-specific rate limits; booking, payment and servicing commands additionally require idempotency.

## Consequences

### Positive

- Implementation can start without supplier production approval.
- Module and public-contract boundaries are testable from the first slice.
- Later mobile clients receive the same behavior as the web client.
- The initial deployment remains small while preserving extraction paths.

### Negative

- Engineers must maintain module-boundary and architecture tests.
- URL major versions require explicit routing and compatibility discipline.
- Blazor SSR calls the public API rather than bypassing it for convenience.

### Risks

Modules can become nominal folders with shared-table coupling. Architecture tests, schema ownership and reviewer checks must enforce the boundary.

## Dependencies

The .NET 10 runtime, identity boundary in ADR-0003, deployment baseline in ADR-0006 and production gates in the [Remaining Review Register](../../decisions/review-register.md).

## Related Documents

- [API Principles](../../api/api-principles.md)
- [Target Architecture](../../architecture/target-architecture.md)
