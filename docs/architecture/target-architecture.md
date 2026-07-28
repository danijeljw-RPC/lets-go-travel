<!-- markdownlint-disable MD013 -->

# Target Architecture

## Status

Accepted. See [ADR-0006](../adr/accepted/ADR-0006-application-and-deployment-baseline.md).

## Foundation

- .NET 10.
- ASP.NET Core Web API as the public application boundary.
- Azure Database for PostgreSQL as managed application persistence.
- Containerised Keycloak for authentication and token issuance.
- Responsive web client first, with later Android and iOS clients using the same API.
- Server-side LiteAPI and later Duffel adapters with backend-only credentials, platform-owned contracts and environment-specific provider activation. Duffel is disabled by default for the initial launch.
- Modular monolith for transactional application behaviour.
- A containerised public API and one general-purpose private worker container initially.
- No serverless functions.

## Logical Modules

Customers, travellers, trips, search, accommodation, flights, pricing, bookings, payments, reconciliation, notifications, documents, support and supplier integrations have explicit code and data ownership even when deployed together. Each module owns its PostgreSQL schema or tables; direct access to another module's private data is prohibited.

## Persistence

Use relational structures for current operational state. Use controlled JSONB for supplier payload evidence where retention is permitted, canonical snapshots, structured differences and versioned extension data. Large binary documents belong in object storage if introduced.

## Background Work

Reconciliation, webhook processing, notification delivery, scheduled reminders, retryable supplier operations and cleanup do not extend customer-facing request latency unnecessarily. The general worker processes durable work through transactional outbox and inbox patterns with idempotency, bounded concurrency and inspectable failure states.

## Deployment Boundary

The frontend, API, worker and Keycloak are container workloads. Workers accept no public application traffic but can use controlled outbound connections. Each US, EU or Australian installation is isolated and has its own Azure PostgreSQL data boundary. Region and container-orchestration selection are separate decisions.

## Scaling Direction

Search is high-volume and replaceable; booking is low-volume, durable and financially sensitive. Monitor and scale them according to their different risk profiles. Split worker deployments or extract a microservice only for measured scaling, isolation, security, availability or ownership needs, and only after establishing exclusive data ownership and a versioned contract.
