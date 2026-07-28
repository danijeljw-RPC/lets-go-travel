<!-- markdownlint-disable MD013 -->

# Issue Index

## Summary

| Status | Count |
| --- | ---: |
| Open | 1 |
| Blocked | 0 |
| In review | 4 |
| Closed | 7 |

## Open Issues

| Issue | Question | Priority | Recommendation |
| --- | --- | --- | --- |
| [OI-0010](open/OI-0010-operational-flight-status-provider.md) | Post-MVP operational flight-status wishlist | p3 | Explicitly non-blocking; evaluate a dedicated provider later. |

## In Review

| Issue | Selected direction | Remaining evidence |
| --- | --- | --- |
| [OI-0002](open/OI-0002-liteapi-commercial-and-merchant-of-record.md) | LiteAPI/provider customer-payment and merchant route. | Written commercial, merchant, settlement, refund, dispute, tax and consumer responsibilities. |
| [OI-0003](open/OI-0003-australian-airline-and-qantas-coverage.md) | LiteAPI for visible Australian carrier content. | Production entitlement, searches, fare completeness, booking, ticketing and servicing. |
| [OI-0005](open/OI-0005-liteapi-webhook-coverage.md) | Durable webhook inbox plus scheduled safety net. | Account event set, authentication, retries, ordering, retention and replay. |
| [OI-0006](open/OI-0006-mobile-payment-and-pci-scope.md) | Official LiteAPI hosted/SDK component where supported. | Platform support, return behaviour and qualified PCI scope. |

## Closed

| Issue | Decision | Closed |
| --- | --- | --- |
| [OI-0001](closed/OI-0001-mvp-product-scope.md) | Hotel, flight and combined hotel-plus-flight journeys are initial scope. | 2026-07-28 |
| [OI-0004](closed/OI-0004-flight-servicing-and-schedule-changes.md) | Partly manual, capability-gated flight servicing with durable reconciliation. | 2026-07-29 |
| [OI-0007](closed/OI-0007-launch-market-locale-and-currency.md) | Australia-first, globally accessible, `en-AU`/AUD defaults and selectable locale. | 2026-07-28 |
| [OI-0008](closed/OI-0008-saved-traveller-and-passport-data.md) | Sensitive reusable traveller data requires explicit opt-in and is off by default. | 2026-07-28 |
| [OI-0009](closed/OI-0009-web-frontend-technology.md) | .NET 10 Blazor SSR with ASP.NET Core Web API. | 2026-07-28 |
| [OI-0012](closed/OI-0012-customer-support-model.md) | First-party ticket support with magic-link guest access and private attachments. | 2026-07-28 |
| [OI-0011](closed/OI-0011-supplier-payload-retention.md) | Canonical evidence with selective short-lived raw payloads and production compliance gates. | 2026-07-29 |

## Decision Order

The product, launch-market, traveller-storage, frontend, flight-servicing, retention and support choices are closed. OI-0002, OI-0003, OI-0005 and OI-0006 retain explicit production evidence gates but do not block provider-neutral implementation or sandbox work. OI-0010 is a non-blocking post-MVP wishlist item.
