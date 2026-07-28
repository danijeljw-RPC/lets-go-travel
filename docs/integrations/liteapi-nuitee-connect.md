<!-- markdownlint-disable MD013 -->

# LiteAPI/Nuitee Connect Integration

## Status

Discovery-stage supplier profile. Documentation evidence was reviewed on 2026-07-27, but account enablement, production access, commercial terms and market suitability remain unconfirmed.

## Documented Capabilities

Official documentation currently describes REST authentication using a confidential `X-API-Key`, hotel search/prebook/book flows, flight search/prebook/book/list flows, a user-payment SDK, and hotel/flight booking lifecycle webhooks. These statements mean the features are documented, not that they are enabled for this project or commercially approved.

## Verified Documentation Evidence

| Topic | Documentation evidence | Project status |
| --- | --- | --- |
| Authentication | API key in `X-API-Key`; vendor says to keep it confidential and out of client code. | Documented; project key/environment not verified. |
| Hotel user payment | SDK flow creates payment data at prebook and returns to the application before final booking. | Documented; web/mobile suitability and responsibilities unresolved. |
| Flight search | Legs-based `/v3.0/flights/rates` search with pricing and itinerary details. | Documented; account and Australian content not verified. |
| Flight prebook/book | Documented prebook and completion endpoints with payment/credit-line variants. | Documented; commercial availability and servicing not verified. |
| Flight list/retrieval | Documented listing and PNR/last-name lookup for persisted bookings. | Documented; schedule-change freshness not established. |
| Webhooks | Documented hotel lifecycle/management events and flight booking lifecycle events where flights are enabled. | Documented; exact account event set, signatures, production-only behaviour and replay guarantees need validation. |

## Official References

- [Authentication](https://docs.liteapi.travel/reference/authentication)
- [User payment](https://docs.liteapi.travel/docs/user-payment)
- [Webhooks](https://docs.liteapi.travel/docs/using-liteapi-webhooks)
- [Revenue management and commission](https://docs.liteapi.travel/docs/revenue-management-and-commission)
- [Flight search](https://docs.liteapi.travel/reference/post_flights-rates)
- [Flight prebook](https://docs.liteapi.travel/reference/post_flights-prebooks)
- [Flight booking](https://docs.liteapi.travel/reference/post_flights-bookings)
- [Flight booking list](https://docs.liteapi.travel/reference/get_flights-bookings)

## Production Evidence Gates

- Production agreement, merchant of record, settlement, markup, commission, parity, chargeback and refund responsibilities.
- Australian hotel and airline coverage, especially Qantas, Jetstar and Virgin Australia.
- Fare source, branded fares, baggage, seats, frequent-flyer numbers and ticket references.
- Voluntary/involuntary changes, cancellations, exchanges, refunds and manual servicing.
- Whether later airline schedule changes appear in retrieved bookings.
- Whether schedule-change-specific events exist beyond documented booking lifecycle events.
- Supported mobile payment approach, Australian payment methods and PCI evidence.
- Webhook authentication strength, ordering, retry, retention and replay.
- Contractual payload retention, content licensing, deletion and dispute-evidence permissions needed to validate OI-0011.

## Related Issues

[OI-0002](../issues/open/OI-0002-liteapi-commercial-and-merchant-of-record.md), [OI-0003](../issues/open/OI-0003-australian-airline-and-qantas-coverage.md), [OI-0004](../issues/open/OI-0004-flight-servicing-and-schedule-changes.md), [OI-0005](../issues/open/OI-0005-liteapi-webhook-coverage.md), and [OI-0006](../issues/open/OI-0006-mobile-payment-and-pci-scope.md).
