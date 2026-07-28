<!-- markdownlint-disable MD013 -->

# Architecture

The accepted architecture is a small-product baseline, not an enterprise platform design. It uses a containerised .NET 10 modular monolith, explicit module and data ownership, managed Azure PostgreSQL, a private general-purpose worker and a dedicated private flight-reconciliation worker.

- [System Context](system-context.md)
- [Target Architecture](target-architecture.md)
- [Trust and Data Boundaries](trust-and-data-boundaries.md)
- [ADR-0009 — MVP Application Contract and Module Foundation](../adr/accepted/ADR-0009-mvp-application-contract-and-module-foundation.md)
- [ADR-0010 — MVP Runtime, Storage and Delivery Baseline](../adr/accepted/ADR-0010-mvp-runtime-storage-and-delivery-baseline.md)

Accepted and future proposed decisions are indexed in [ADRs](../adr/index.md).
