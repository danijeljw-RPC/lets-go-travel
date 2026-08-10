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
- A containerised public API, one general-purpose private worker container and a dedicated private flight-reconciliation worker container.
- No serverless functions.

## Logical Modules

Customers, travellers, trips, search, accommodation, flights, pricing, bookings, payments, reconciliation, notifications, documents, support, supplier integrations, and retention/legal hold have explicit code and data ownership even when deployed together. Each module owns its PostgreSQL schema or tables; direct access to another module's private data is prohibited. The retention module owns only the concepts genuinely shared across modules (legal holds, the retention policy catalog, deletion receipts); each other module implements its own retention sweep against its own schema and depends on the retention module, never the reverse.

## Persistence

Use relational structures for current operational state. Use controlled JSONB for supplier payload evidence where retention is permitted, canonical snapshots, structured differences and versioned extension data. Large binary documents belong in object storage if introduced.

Object storage is the expected home for vouchers, invoices, receipts, itinerary documents, approved customer uploads, generated trip packs and supplier documents whose retention is permitted. Large binary content should not normally be stored directly in PostgreSQL.

Every persistent record class follows [Data Retention and Legal Hold](../security/data-retention-and-legal-hold.md). Raw supplier payload retention is disabled by default, primary expiry processing runs at least daily, ordinary backup copies expire within 35 days and any matter-specific legal hold uses a separate protected hold process rather than indefinite routine backups.

Support ticket state and immutable message threads belong to the support module's PostgreSQL boundary. Each ticket uses an internal UUIDv7 identifier plus a separately generated high-entropy guest bearer token whose one-way hash is stored. Ticket attachments are private S3-compatible objects; authorised requests use short-lived signed access or application streaming rather than permanent public URLs.

Introduce distributed caching only when measurements justify it. Suitable candidates include static supplier content, airport/airline/country/city reference data, destination metadata, short-lived search responses, supplier capability metadata and display exchange rates. Durable booking state, payment decisions and authoritative cancellation state never use a cache as their source of truth.

## Background Work

Reconciliation, webhook processing, notification delivery, scheduled reminders, retryable supplier operations and cleanup do not extend customer-facing request latency unnecessarily. The general worker processes shared durable work through transactional outbox and inbox patterns with idempotency, bounded concurrency and inspectable failure states.

The dedicated flight-reconciliation worker uses durable PostgreSQL work records rather than an operating-system crontab. It checks active future flight bookings at least daily outside the final 24 hours before departure and at least hourly during the final 24 hours before each affected segment. Authenticated webhooks may enqueue immediate checks. Meaningful changes create immutable versions and notification outbox events under [ADR-0008](../adr/accepted/ADR-0008-durable-flight-reconciliation-and-customer-notification.md).

## Deployment Boundary

The frontend, API, workers and Keycloak are container workloads. Workers accept no public application traffic but can use controlled outbound connections. The Australia-first production environment uses Azure Container Apps and Azure Database for PostgreSQL in Australia East. Any later regional installation is isolated and has its own PostgreSQL data boundary. See [ADR-0010](../adr/accepted/ADR-0010-mvp-runtime-storage-and-delivery-baseline.md).

## Scaling Direction

Search is high-volume and replaceable; booking is low-volume, durable and financially sensitive. Monitor and scale them according to their different risk profiles. Split worker deployments or extract a microservice only for measured scaling, isolation, security, availability or ownership needs, and only after establishing exclusive data ownership and a versioned contract.

Supplier APIs remain external, fallible fulfilment systems. The platform contract must remain stable through supplier field changes, expired offers, pending outcomes, duplicate webhooks, unexpected payloads, provider outages and the introduction of another supplier.
