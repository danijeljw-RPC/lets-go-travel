---
adr_id: ADR-0010
title: MVP Runtime Storage and Delivery Baseline
status: accepted
date_proposed: 2026-07-29
date_accepted: 2026-07-29
date_rejected: null
date_superseded: null
supersedes: []
decision_owners:
  - Architecture
  - Engineering
  - Operations
related_issues:
  - OI-0004
  - OI-0005
  - OI-0011
related_adrs:
  - ADR-0004
  - ADR-0006
  - ADR-0008
  - ADR-0009
related_plans:
  - PLAN-0001
related_docs:
  - docs/architecture/target-architecture.md
  - docs/operations/mvp-operational-policy.md
---

<!-- markdownlint-disable MD013 MD025 -->

# ADR-0010 — MVP Runtime, Storage and Delivery Baseline

## Status

Accepted on 2026-07-29.

## Context

The target architecture left orchestration, Australian region, object storage, scanning, secrets, observability and delivery tooling for technical planning. These choices no longer require a product-scope decision and must be explicit before the first implementation plan.

## Decision Drivers

- Australia-first deployment and privacy baseline.
- Four container workloads without Kubernetes administration.
- PostgreSQL-backed durable work and no serverless function dependency.
- S3-compatible private attachment storage.
- Low operational complexity for a small consumer product.
- Portable application contracts and standard telemetry.

## Options Considered

### Option A — Managed Azure application runtime with portable supplier/storage boundaries

Use Azure Container Apps and Azure Database for PostgreSQL in Australia East, Azure Key Vault, OpenTelemetry and a production S3-compatible object store in an Australian region.

### Option B — Self-managed Kubernetes

Run the platform, Keycloak, databases and supporting services on a managed Kubernetes cluster.

### Option C — Single virtual machine

Run all containers and stateful services on one host.

## Decision

Choose Option A.

- Production workloads run as Azure Container Apps in Australia East. API and web ingress are public through the approved edge; Keycloak has only its required authentication ingress; both workers have no public ingress.
- Azure Database for PostgreSQL is the authoritative application store. Durable schedules, inboxes, outboxes, leases and retries use PostgreSQL rather than operating-system cron or an in-memory queue.
- Azure Key Vault stores production secrets. Local development uses .NET user secrets and ignored environment files; secrets never enter committed configuration.
- OpenTelemetry is the application instrumentation boundary. Production exports to Azure Monitor/Application Insights; local development may export OTLP to a local collector.
- Distributed caching is not introduced initially. In-memory caching is permitted only for replaceable reference/search data with explicit expiry. Booking, payment, refund and cancellation state never depends on a cache.
- The object-storage interface is S3-compatible. Local development uses MinIO. Production uses Amazon S3 in `ap-southeast-2` unless a later S3-compatible Australian-region service passes the same contract and security review.
- Ticket uploads are private and quarantined until content-type, extension, size and malware checks pass. MVP limits are 10 MiB per file, five files per message and 50 MiB per ticket. Allowed launch types are PDF, JPEG, PNG and UTF-8 plain text. Downloads use application authorisation followed by a signed URL valid for at most five minutes.
- ClamAV provides the initial malware-scanning adapter. A scan failure or unavailable scanner leaves the object quarantined and unavailable; it never fails open.
- GitHub Actions builds, tests, produces containers, scans dependencies/images and promotes immutable images through environments. Production deployment requires protected-environment approval and completed production gates.
- No general-purpose service bus is introduced for the MVP. PostgreSQL durable work is the baseline; extraction requires measured need and a versioned contract.
- Privileged support capability is a protected Blazor SSR area backed by Keycloak roles and platform permissions. Impersonation is disabled for the MVP.

## Consequences

### Positive

- The runtime matches the accepted container and worker model.
- Australian application data has an explicit primary region.
- Local dependencies remain reproducible through containers.
- Application telemetry, storage and secrets have replaceable boundaries.

### Negative

- Production uses both Azure and AWS for the selected S3-compatible storage requirement.
- ClamAV and MinIO add local container dependencies.
- PostgreSQL-backed work requires careful leasing, retry and maintenance design.

### Risks

Regional availability, cost or supplier agreements may require substituting an equivalent service. The application must depend on contracts rather than provider-specific behavior, and production provisioning must re-check current regional availability.

## Dependencies

Production activation still requires the supplier, legal, PCI and operational evidence in the [Remaining Review Register](../../decisions/review-register.md).

## Related Documents

- [Target Architecture](../../architecture/target-architecture.md)
- [MVP Operational Policy](../../operations/mvp-operational-policy.md)
