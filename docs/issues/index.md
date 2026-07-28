<!-- markdownlint-disable MD013 -->

# Issue Index

## Summary

| Status | Count |
| --- | ---: |
| Open | 12 |
| Blocked | 0 |
| In review | 0 |
| Closed | 0 |

## Open Issues

| Issue | Question | Priority | Recommendation |
| --- | --- | --- | --- |
| [OI-0001](open/OI-0001-mvp-product-scope.md) | First sellable product scope | p0 | Hotel-first transactional MVP. |
| [OI-0002](open/OI-0002-liteapi-commercial-and-merchant-of-record.md) | LiteAPI commercial and merchant-of-record model | p0 | Prefer supplier-side MOR if contractually available and clear. |
| [OI-0003](open/OI-0003-australian-airline-and-qantas-coverage.md) | Australian airline and Qantas coverage | p0 | Defer flight selling until account-level evidence exists. |
| [OI-0004](open/OI-0004-flight-servicing-and-schedule-changes.md) | Flight servicing and schedule-change behaviour | p0 | Plan explicit manual fallback; do not sell until boundaries are proven. |
| [OI-0005](open/OI-0005-liteapi-webhook-coverage.md) | Webhook coverage and delivery guarantees | p0 | Webhook-triggered reconciliation plus scheduled safety net. |
| [OI-0006](open/OI-0006-mobile-payment-and-pci-scope.md) | Web/mobile payment and PCI scope | p0 | Prefer full redirect; otherwise approved embedded component after review. |
| [OI-0007](open/OI-0007-launch-market-locale-and-currency.md) | Launch market, locale and currency | p1 | Australia, `en-AU`, AUD first. |
| [OI-0008](open/OI-0008-saved-traveller-and-passport-data.md) | Saved traveller and passport policy | p1 | Minimal saved traveller; no saved passport in MVP. |
| [OI-0009](open/OI-0009-web-frontend-technology.md) | Web frontend technology | p1 | Prefer Blazor if team/payment fit; otherwise Razor Pages for smallest footprint. |
| [OI-0010](open/OI-0010-operational-flight-status-provider.md) | Operational flight-status provider | p2 | Defer from MVP and keep separate from booking data. |
| [OI-0011](open/OI-0011-supplier-payload-retention.md) | Supplier payload retention | p1 | Canonical history plus selective short-lived protected raw evidence. |
| [OI-0012](open/OI-0012-customer-support-model.md) | MVP support model | p1 | Email/ticket support with urgent escalation. |

## Decision Order

Resolve OI-0001, OI-0002, OI-0007 and OI-0012 first. Flight-specific issues can proceed in parallel without blocking the recommended hotel-first MVP. OI-0006 must be resolved before checkout planning, and OI-0008/OI-0011 before finalising the data model.
