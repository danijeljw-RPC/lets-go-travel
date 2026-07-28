<!-- markdownlint-disable MD013 -->

# Issue Index

## Summary

| Status | Count |
| --- | ---: |
| Open | 2 |
| Blocked | 0 |
| In review | 5 |
| Closed | 5 |

## Open Issues

| Issue | Question | Priority | Recommendation |
| --- | --- | --- | --- |
| [OI-0010](open/OI-0010-operational-flight-status-provider.md) | Post-MVP operational flight-status wishlist | p3 | Explicitly non-blocking; evaluate a dedicated provider later. |
| [OI-0011](open/OI-0011-supplier-payload-retention.md) | Supplier payload retention | p1 | Canonical history plus selective short-lived protected raw evidence. |

## In Review

| Issue | Selected direction | Remaining evidence |
| --- | --- | --- |
| [OI-0002](open/OI-0002-liteapi-commercial-and-merchant-of-record.md) | LiteAPI/provider customer-payment and merchant route. | Written commercial, merchant, settlement, refund, dispute, tax and consumer responsibilities. |
| [OI-0003](open/OI-0003-australian-airline-and-qantas-coverage.md) | LiteAPI for visible Australian carrier content. | Production entitlement, searches, fare completeness, booking, ticketing and servicing. |
| [OI-0004](open/OI-0004-flight-servicing-and-schedule-changes.md) | Dedicated durable reconciliation with customer notifications. | Retrieval freshness, schedule propagation, servicing and escalation matrix. |
| [OI-0005](open/OI-0005-liteapi-webhook-coverage.md) | Durable webhook inbox plus scheduled safety net. | Account event set, authentication, retries, ordering, retention and replay. |
| [OI-0006](open/OI-0006-mobile-payment-and-pci-scope.md) | Official LiteAPI hosted/SDK component where supported. | Platform support, return behaviour and qualified PCI scope. |

## Closed

| Issue | Decision | Closed |
| --- | --- | --- |
| [OI-0001](closed/OI-0001-mvp-product-scope.md) | Hotel, flight and combined hotel-plus-flight journeys are initial scope. | 2026-07-28 |
| [OI-0007](closed/OI-0007-launch-market-locale-and-currency.md) | Australia-first, globally accessible, `en-AU`/AUD defaults and selectable locale. | 2026-07-28 |
| [OI-0008](closed/OI-0008-saved-traveller-and-passport-data.md) | Sensitive reusable traveller data requires explicit opt-in and is off by default. | 2026-07-28 |
| [OI-0009](closed/OI-0009-web-frontend-technology.md) | .NET 10 Blazor SSR with ASP.NET Core Web API. | 2026-07-28 |
| [OI-0012](closed/OI-0012-customer-support-model.md) | First-party ticket support with magic-link guest access and private attachments. | 2026-07-28 |

## Decision Order

The product, launch-market, traveller-storage, frontend and support choices are closed. Resolve the external evidence for OI-0002 through OI-0006 before production flight/payment activation. OI-0010 is a non-blocking post-MVP wishlist item. OI-0011 remains the open data-retention decision and must not be answered implicitly during implementation.
