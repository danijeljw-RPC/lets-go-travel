<!-- markdownlint-disable MD013 -->

# Remaining Review Register

## Status

Repository-wide documentation review completed on 2026-07-28. Product direction is sufficient to continue MVP implementation planning. The items below require external evidence, specialist approval or later technical selection; they are not unanswered product-scope questions unless stated otherwise.

## Production Activation Reviews

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| OI-0002 LiteAPI commercial/MOR | Executed terms covering merchant, settlement, statement descriptor, commission/margin, refunds, disputes, chargebacks, tax and booking-failure ownership. | Product, Finance, Legal/Compliance | Blocks production payment and booking activation, not provider-neutral implementation. |
| OI-0003 Australian carrier capability | Account-level production entitlement plus dated search, fare, booking, ticket and servicing evidence for Qantas, Jetstar, Virgin Australia and intended markets. | Product, Supplier Integration | Blocks exposing unsupported flight inventory, not hotel or capability-gated flight implementation. |
| OI-0004 flight servicing | Retrieval freshness, schedule-change propagation, cancellation, exchange, refund, manual escalation contacts, hours, fees and SLA. | Product, Operations, Supplier Integration | Blocks the customer flight promise where unsupported. |
| OI-0005 webhook guarantees | Account event catalogue, authentication, ordering, duplication, retry, retention, replay and environment differences. | Architecture, Security, Supplier Integration | Does not block durable inbox/reconciliation implementation; blocks production webhook reliance. |
| OI-0006 payment/PCI | Supported Blazor/browser component, mobile deferral, return behaviour, 3-D Secure, Australian payment methods, provider AOC and qualified PCI scope. | Finance, Security, Compliance | Blocks production checkout activation. |
| OI-0011 retention validation | LiteAPI retention/licensing terms and Australian legal/privacy approval of the baseline schedule, record classification and trigger dates. | Data, Privacy, Legal/Compliance | Does not block implementation of the stricter defaults; blocks claiming final production compliance. |
| Australian market legal pack | Consumer-law/pricing, privacy, travel-selling/licensing, insolvency/trust, cross-border/data-residency, minor-traveller and breach-response advice. | Legal/Compliance, Product | Blocks production launch and final customer terms, not application scaffolding. |

## Operational Reviews Before Launch

| Review | Decision or evidence needed | Effect |
| --- | --- | --- |
| Customer-support operations | Published service hours, urgent-travel criteria, supplier escalation contacts and response expectations. | Required before accepting live customer bookings. |
| Change notification policy | Materiality/severity thresholds, quiet-hour exceptions, customer wording and support escalation rules. | Required before automated itinerary-change notifications are enabled. |
| Recovery objectives | Approved PostgreSQL, Keycloak and object-storage RPO/RTO plus restore and post-outage reconciliation exercises. | Required before production readiness approval. |
| Retention operations | Evidence that expiry, backup tombstones, hold export/release and deletion receipts work in the selected services. | Required before personal/supplier data enters production. |

## Technical Planning Reviews

These choices can be made by architecture/engineering during the implementation plan without another product-scope decision:

- API versioning mechanism, deprecation window and later mobile-client support window.
- Container orchestration, Australian deployment region, durable scheduling/background-work framework and outbox/inbox implementation.
- Object-storage provider, malware-scanning path, ticket upload limits and signed-download lifetime.
- Cache, secret-management, observability and privileged-administration mechanisms.
- Complete happy/failure-path state machines, canonicalisation versioning, `DiffJson` schema and notification severity implementation.
- CI/CD, environment promotion, supplier sandbox certification and production-readiness evidence collection.

## Settled Non-blocking Deferrals

- OI-0010 live operational flight status is a post-MVP wishlist item and does not block the MVP.
- Native Android/iOS applications, push/SMS, offline trip packs, trip sharing, loyalty, AI planning and manually added itinerary items are deferred from the initial transactional release.
- Duffel customer inventory remains disabled until its separate production settlement gate is satisfied.

## No Further Product-owner Answer Currently Required

The repository review found no remaining MVP scope question that requires another immediate product-owner answer. Work may continue on the accepted hotel, flight and combined journey, Blazor SSR, LiteAPI-preferred payment, traveller opt-in, reconciliation, localisation and ticket-support direction while the production reviews above are collected.
