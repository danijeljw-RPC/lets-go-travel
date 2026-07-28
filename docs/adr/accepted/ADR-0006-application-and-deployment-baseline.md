---
adr_id: ADR-0006
title: Application and Deployment Baseline
status: accepted
date_proposed: 2026-07-27
date_accepted: 2026-07-28
date_rejected: null
date_superseded: null
superseded_by: null
supersedes: []
decision_owners:
  - Architecture
  - Platform Engineering
related_issues:
  - OI-0009
related_adrs:
  - ADR-0002
  - ADR-0003
related_plans:
  - PLAN-0001
related_docs:
  - docs/architecture/target-architecture.md
---

# ADR-0006 — Application and Deployment Baseline

## Status

Accepted on 2026-07-28.

## Context

The project needs a small initial application and deployment baseline without preventing modules from being scaled or extracted later. Customer-facing requests, durable booking changes, supplier reconciliation and outbound activities have different runtime characteristics, but splitting them into microservices from the start would introduce distributed-system overhead before it is justified.

Deployments may run in US, EU or Australian datacentres. Each regional installation is isolated from the others and keeps its data in its own Azure Database for PostgreSQL. Region selection and the container orchestration platform are separate deployment decisions.

## Decision Drivers

- Existing .NET direction.
- Small team and discovery-stage domain.
- Transactional booking consistency.
- Web-first delivery and later mobile clients using the same API.
- Durable background work that does not extend customer-facing request latency.
- Containerised workloads without serverless cold-start and platform constraints.
- Explicit module and data ownership that supports later extraction.
- Supplier-neutral client behaviour across LiteAPI and later providers such as Duffel.

## Options Considered

### Option A — Containerised .NET 10 modular monolith with API and worker

Use a modular .NET 10 application deployed initially as a public ASP.NET Core API container and one private general-purpose worker container. Run the frontend and Keycloak as containers and use Azure Database for PostgreSQL as managed persistence. Enforce module-owned data boundaries and use durable database-backed coordination for asynchronous work.

### Option B — Microservices from the start

Deploy identity integration, search, bookings, payments, notifications and reconciliation as separate services.

### Option C — Serverless functions by workflow

Build each supplier and booking operation as independently deployed functions.

## Recommendation

Choose Option A. It fits the product size and consistency needs, avoids serverless cold-start and execution-model constraints, and preserves a controlled path to dedicated workers or microservices when measured scaling, isolation, security, availability or ownership needs emerge.

## Decision

Option A is accepted.

The initial regional installation consists of a public API container, one general-purpose private worker container, a containerised Keycloak deployment, the selected containerised web frontend and Azure Database for PostgreSQL. The worker accepts no public application traffic, connects privately to PostgreSQL and may make controlled outbound calls to suppliers, email providers and other approved services.

Each US, EU or Australian installation is deployed and operated separately with its own data boundary. This ADR does not select a region, container orchestrator or web frontend technology.

Serverless functions are excluded from the application baseline. Additional workload-specific worker containers or microservices are introduced only when justified by evidence rather than in anticipation of future scale.

## Module and Data Ownership

Customers, travellers, trips, search, accommodation, flights, pricing, bookings, payments, reconciliation, notifications, documents, support and supplier integrations are explicit modules. Each module owns its application commands, domain logic, persistence code and PostgreSQL schema or tables.

API and worker processes access a module's data only through that module's application and persistence boundaries. A module cannot query or update another module's private tables. Cross-module effects use explicit commands or transactionally recorded outbox events.

LiteAPI and later suppliers such as Duffel use separate provider adapters behind platform-owned contracts. Supplier capabilities and provenance remain explicit internally while clients consume supplier-neutral platform models.

## Background Work and Failure Handling

The initial general-purpose worker processes reconciliation, authenticated webhooks, scheduled supplier updates, notifications, retries, outbox delivery and cleanup. API state changes and their outbox events are committed in one PostgreSQL transaction. Incoming webhooks are persisted to an inbox, deduplicated and processed asynchronously rather than blindly overwriting current state.

Background handlers are idempotent, concurrency-safe and resumable after container restarts. Work records retain status, attempts, timing, correlation identifiers and failure details. Supplier timeouts do not prove that an operation failed, so financially or operationally significant operations reconcile supplier state before retry. Repeated failures enter an inspectable failed state instead of retrying indefinitely.

The API and worker use separate workload identities and PostgreSQL roles with least privilege. Supplier credentials and other secrets remain outside container images and database records.

## Scaling and Extraction

The general worker may be split into workload-specific container deployments when independent scaling, resource limits, failure isolation, security, availability or operational ownership requires it. A module becomes a genuine microservice only after it has exclusive data ownership and a versioned service or event contract; direct shared-table access is not an acceptable extraction boundary.

Architecture tests must prevent forbidden cross-module references and data access. Verification must cover PostgreSQL integration, outbox/inbox duplication and concurrency, worker interruption and restart, supplier adapter contracts, container health and shutdown, migration compatibility during overlapping API/worker versions and recovery from poisoned work items or ambiguous supplier outcomes.

## Consequences

### Positive

- Lower operational and development overhead.
- Easier transactional consistency and local development.
- Explicit, testable module and data ownership.
- Background workloads can scale independently when evidence justifies it.
- Supplier adapters can add Duffel or other providers without changing client contracts.
- Regional installations preserve an isolated data boundary.

### Negative

- Search and booking initially share the API deployment and database server.
- The general worker initially shares one deployment cadence across background workloads.
- Module-boundary enforcement and durable work coordination add design and test obligations.

### Risks

Poor module discipline or unrestricted shared-table access could create a distributed monolith that is difficult to extract. Database outages affect both the API and worker. Misconfigured worker egress or credentials could expose supplier or customer data. These risks require automated architecture checks, least-privilege identities, controlled networking and tested recovery behaviour.

## Dependencies

The web frontend choice remains open in [OI-0009](../../issues/open/OI-0009-web-frontend-technology.md). Container orchestration, background-work framework, regional deployment selection and detailed infrastructure topology are deferred to implementation planning or dedicated decisions.

## Related Documents

- [Target Architecture](../../architecture/target-architecture.md)
