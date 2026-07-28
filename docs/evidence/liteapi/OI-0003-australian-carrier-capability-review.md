# OI-0003 — Australian Carrier Capability Evidence Review

**Status:** Partially evidenced — Australian carrier search capability observed; production and end-to-end carrier capability still require dated confirmation
**Vendor:** LiteAPI / Nuitee Connect
**Reviewed:** 28 July 2026
**Decision owners:** Product, Supplier Integration

## Purpose

This document records the evidence relevant to the following open issue:

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| OI-0003 Australian carrier capability | Account-level production entitlement plus dated search, fare, booking, ticket and servicing evidence for Qantas, Jetstar, Virgin Australia and intended markets. | Product, Supplier Integration | Blocks exposing unsupported flight inventory, not hotel or capability-gated flight implementation. |

## Direct answer

Searches performed through the LiteAPI API and LiteAPI console have returned flight inventory for:

| Carrier | IATA code | Observed capability |
| --- | --- | --- |
| Qantas | `QF` | Search results and fare offers returned |
| Jetstar | `JQ` | Search results and fare offers returned |
| Virgin Australia | `VA` | Search results and fare offers returned |

This is valid evidence that the current LiteAPI account and environment can discover offers involving the three required Australian carriers for the routes, travel dates, passenger configuration, currency and point of sale used during testing.

This evidence is sufficient to support:

- implementation of carrier-neutral flight search;
- display logic that is capability-gated by actual search results;
- continued integration against LiteAPI's flight search, verification, prebook and booking interfaces; and
- recording Qantas, Jetstar and Virgin Australia as **observed search carriers**, rather than assumed unsupported carriers.

It is not, by itself, complete evidence of:

- production-account entitlement;
- availability across every intended Australian or international market;
- successful offer verification;
- successful provider reservation or prebook;
- confirmed booking;
- ticket issuance or ticket-number availability;
- cancellation, refund, change or other servicing capability; or
- consistent capability for every fare, route, operating carrier, marketing carrier or underlying LiteAPI provider.

OI-0003 should therefore be treated as **partially satisfied**, not fully closed, unless the searches were performed using an approved production API key and the downstream booking and servicing lifecycle has also been tested.

## Public LiteAPI evidence

LiteAPI's public documentation supports the following conclusions.

### Flight search, pricing and booking are supported platform capabilities

LiteAPI states that its Flights API allows an integration to search, price and book flights through Nuitee Connect. It also lists the ability to retrieve fare details, present flight options, select seats, add baggage and complete bookings.

Reference:

- [Getting Access to Flights](https://docs.liteapi.travel/docs/getting-access-to-flights)

### Inventory is aggregated from multiple flight distribution channels

LiteAPI states that Nuitee Connect aggregates real-time flight inventory from GDS, NDC and low-cost-carrier providers into one API.

This is consistent with Qantas, Jetstar and Virgin Australia offers appearing through a common search response, but LiteAPI's public documentation does not publish a carrier-by-carrier production support guarantee.

Reference:

- [Build a Flight Booking Experience](https://docs.liteapi.travel/docs/build-a-flight-booking-experience)

### Search results contain live offers and fare information

LiteAPI documents `POST /v3.0/flights/rates` as returning real-time pricing from multiple providers. Results can include:

- journeys and segments;
- offers from multiple providers;
- fare, tax and fee breakdowns;
- baggage information;
- cabin class;
- one-way, return and multi-city itineraries; and
- filters for stops, refundability, time and price.

Reference:

- [Search for flights](https://docs.liteapi.travel/reference/post_flights-rates)

### Search evidence is route-, date- and market-specific

LiteAPI requires each search to include itinerary legs containing origin, destination and departure date. It also recommends providing currency and country as the point of sale because pricing and returned offers depend on the selected market.

A successful carrier result therefore proves capability for the tested request. It should not be interpreted as a permanent or universal carrier entitlement.

References:

- [Search for flights](https://docs.liteapi.travel/reference/post_flights-rates)
- [Build a Flight Booking Experience](https://docs.liteapi.travel/docs/build-a-flight-booking-experience)

### Production flight access requires explicit approval

LiteAPI explicitly states that:

- flight production access is not enabled by default;
- production access requires approval;
- the Nuitee team must activate access because of provider restrictions, rate limits and commercial considerations; and
- a working sandbox integration may be required before approval.

This means successful console or sandbox searches do not automatically establish account-level production entitlement.

Reference:

- [Getting Access to Flights](https://docs.liteapi.travel/docs/getting-access-to-flights)

### Sandbox inventory does not prove production inventory

LiteAPI states that sandbox flight data:

- does not mirror production;
- may have limited pricing and availability accuracy;
- may return incomplete or inconsistent results; and
- should not be used to validate real-world pricing or availability.

Where the Qantas, Jetstar and Virgin Australia evidence came from sandbox, it demonstrates API compatibility and observed carrier search output, but not production supply approval.

Reference:

- [Getting Access to Flights](https://docs.liteapi.travel/docs/getting-access-to-flights)

### LiteAPI documents an end-to-end booking flow

LiteAPI documents the following dependency chain:

`Search → Select → Verify → Prebook → Add Extras → Book → Confirm`

The guide describes the flow as progressing from search to a confirmed ticket. The booking endpoint returns a confirmed booking, provider confirmation reference, itinerary, passengers and payment details.

References:

- [Build a Flight Booking Experience](https://docs.liteapi.travel/docs/build-a-flight-booking-experience)
- [Create a flight prebook](https://docs.liteapi.travel/reference/post_flights-prebooks)
- [Complete a flight booking](https://docs.liteapi.travel/reference/post_flights-bookings)

### Confirmed bookings can be retrieved and matched to a PNR

LiteAPI provides flight-booking retrieval endpoints that expose:

- booking status;
- itinerary and flight segments;
- passenger details;
- provider confirmation reference;
- pricing;
- an airline PNR lookup using airline PNR and passenger surname; and
- confirmed bookings owned by the authenticated API user.

This supports implementation of booking retrieval and status checking. It does not publicly guarantee that every carrier returns an e-ticket number or supports every servicing operation.

References:

- [Get flight booking details](https://docs.liteapi.travel/reference/get_flights-bookings-bookingid)
- [List flight bookings](https://docs.liteapi.travel/reference/get_flights-bookings)

### Flight lifecycle events are account-capability dependent

LiteAPI's webhook documentation states that flight booking lifecycle events are available where flight booking is enabled on the account. This reinforces that search access and full booking lifecycle access may be separately constrained by account configuration.

Reference:

- [Using Nuitee Connect webhooks](https://docs.liteapi.travel/docs/using-liteapi-webhooks)

## Evidence assessment

| Required evidence | Current evidence | Assessment |
| --- | --- | --- |
| Qantas search | Qantas offers observed through LiteAPI API and console. | Satisfied for the tested environment, request and date. |
| Qantas fare | Fare offers observed in search results. | Satisfied at search-response level. Verify response should also be retained before production approval. |
| Qantas booking | No completed carrier-specific booking evidence supplied in this review. | Outstanding. |
| Qantas ticket | No carrier-specific ticket or confirmed-ticket evidence supplied in this review. | Outstanding. |
| Qantas servicing | No carrier-specific cancellation, refund, change or support evidence supplied in this review. | Outstanding. |
| Jetstar search | Jetstar offers observed through LiteAPI API and console. | Satisfied for the tested environment, request and date. |
| Jetstar fare | Fare offers observed in search results. | Satisfied at search-response level. Verify response should also be retained before production approval. |
| Jetstar booking | No completed carrier-specific booking evidence supplied in this review. | Outstanding. |
| Jetstar ticket | No carrier-specific ticket or confirmed-ticket evidence supplied in this review. | Outstanding. |
| Jetstar servicing | No carrier-specific cancellation, refund, change or support evidence supplied in this review. | Outstanding. |
| Virgin Australia search | Virgin Australia offers observed through LiteAPI API and console. | Satisfied for the tested environment, request and date. |
| Virgin Australia fare | Fare offers observed in search results. | Satisfied at search-response level. Verify response should also be retained before production approval. |
| Virgin Australia booking | No completed carrier-specific booking evidence supplied in this review. | Outstanding. |
| Virgin Australia ticket | No carrier-specific ticket or confirmed-ticket evidence supplied in this review. | Outstanding. |
| Virgin Australia servicing | No carrier-specific cancellation, refund, change or support evidence supplied in this review. | Outstanding. |
| Production flight entitlement | LiteAPI documents that production flights require explicit approval. No account-specific approval has been included in this review. | Outstanding unless a production approval message, contract or dashboard entitlement is attached. |
| Intended-market coverage | Search evidence proves only the routes, dates, point of sale, currency and passenger configuration tested. | Partially evidenced. A dated market matrix is still required. |

## Recommended evidence record

The following information should be retained for every carrier test:

| Field | Required value |
| --- | --- |
| Test date and time | UTC timestamp and local Australian timestamp |
| Environment | Sandbox or production |
| API account | Non-secret account identifier |
| API key classification | Sandbox or production; never record the key value |
| Interface | API, LiteAPI console or whitelabel |
| Carrier | Marketing and operating carrier |
| IATA code | `QF`, `JQ` or `VA` |
| Origin | Airport or city IATA code |
| Destination | Airport or city IATA code |
| Travel date | Exact departure date |
| Point of sale | Country supplied to LiteAPI |
| Currency | Currency supplied to LiteAPI |
| Passengers | Adult, child and infant configuration |
| Search result | Journey and offer returned |
| Fare evidence | Total, base fare, taxes, fees, cabin and fare family where provided |
| Provider | Provider identifier where exposed |
| Verify result | Offer still available and any price change |
| Prebook result | Provider reservation and `prebookId` created |
| Booking result | LiteAPI `bookingId` and provider booking reference returned |
| Ticket result | Ticket status and ticket number where available |
| Servicing result | Retrieval, cancellation, refund, change and ancillary behaviour |
| Evidence location | Sanitised JSON, screenshot, test report or issue attachment |

Secrets, payment credentials, passenger documents and personal information must be removed from retained evidence.

## Recommended capability matrix

| Carrier | Search | Fare | Verify | Prebook | Book | Ticket | Retrieve | Cancel/refund | Change/service | Production-approved |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Qantas (`QF`) | Observed | Observed in search | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet evidenced |
| Jetstar (`JQ`) | Observed | Observed in search | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet evidenced |
| Virgin Australia (`VA`) | Observed | Observed in search | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet recorded | Not yet evidenced |

The matrix should be updated from dated execution evidence rather than vendor assumptions.

## Recommended OI-0003 entry

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| OI-0003 Australian carrier capability | Qantas (`QF`), Jetstar (`JQ`) and Virgin Australia (`VA`) offers have been observed through the LiteAPI API and console, establishing search and search-fare capability for the tested routes, dates, market and current account environment. LiteAPI publicly documents a complete search-to-confirmed-ticket flight flow, but also states that production flight access requires explicit account approval and that sandbox inventory does not mirror production. Before exposing a carrier in production, retain account-level production approval and dated carrier-by-carrier evidence for search, fare verification, prebook, confirmed booking, ticket status or ticket reference where supplied, booking retrieval, cancellation/refund behaviour and required servicing in each intended point-of-sale market. | Product, Supplier Integration | Does not block hotel implementation or provider-neutral, capability-gated flight implementation. Blocks advertising or exposing a carrier as production-supported until the required production entitlement and carrier lifecycle evidence have been recorded. |

## Recommended decision status

**Keep OI-0003 open with the search portion marked as evidenced.**

The current evidence should be recorded as:

> On 28 July 2026, Qantas, Jetstar and Virgin Australia flight offers were successfully returned through the LiteAPI API and LiteAPI console. This confirms observed search and fare-result capability in the tested LiteAPI account environment. It does not, without additional evidence, confirm production entitlement or carrier-specific booking, ticketing and servicing capability.

OI-0003 may be closed when both of the following are complete:

1. **Production entitlement:** LiteAPI has explicitly enabled flight production access for the account and the approval is retained.
2. **Dated end-to-end evidence:** each carrier required for launch has passed the agreed search, verify, prebook, book, confirm/ticket, retrieve and servicing tests for the intended Australian point of sale and launch markets.

## Implementation consequence

Provider-neutral flight implementation may proceed.

The application should expose inventory based on returned and verified capabilities, rather than a permanent assumption that a carrier is supported.

Recommended controls:

- maintain a carrier capability registry by environment and point of sale;
- distinguish `ObservedInSearch`, `Verified`, `Bookable`, `Ticketed`, `Retrievable`, `Cancellable` and `Serviceable`;
- do not mark a carrier production-supported from airline-directory metadata alone;
- do not infer booking capability from search capability;
- require offer verification before checkout;
- retain the marketing carrier, operating carrier and provider identifiers;
- feature-gate carriers or markets that have not passed production evidence;
- handle offers disappearing or changing price between search and verify;
- handle provider-specific ancillaries and fare rules as optional capabilities;
- avoid promising self-service cancellation or changes unless tested for that carrier and fare; and
- preserve a support path where a provider booking enters a non-confirmed or exceptional state.

## Vendor questions still requiring confirmation

1. Has flight production access been explicitly enabled for the account?
2. Are Qantas, Jetstar and Virgin Australia enabled for the intended Australian point of sale in production?
3. Are there route, country, currency, distribution-channel or fare-type restrictions for each carrier?
4. Which source supplies each carrier: GDS, NDC, LCC connection or another aggregator?
5. Does LiteAPI support both marketing-carrier and operating-carrier identification?
6. Does a successful booking response mean the booking is ticketed, or can ticketing remain pending?
7. Are airline ticket numbers returned for Qantas, Jetstar and Virgin Australia?
8. Which booking statuses may occur between reservation, confirmation and ticketing?
9. Can each carrier booking be retrieved using booking ID and airline PNR?
10. Which carriers and fare types support API cancellation?
11. Are refunds automated or manually serviced?
12. Are voluntary date, flight or passenger-name changes supported?
13. How are involuntary schedule changes and carrier cancellations communicated?
14. Are seats and baggage supported for each carrier and fare type?
15. Are there carrier-specific payment, fraud, ticketing deadline or support requirements?
16. Which flight webhook events are enabled for the production account?
17. What production evidence does LiteAPI require before permitting launch?

## Reference register

| Reference | Relevance |
| --- | --- |
| [Getting Access to Flights](https://docs.liteapi.travel/docs/getting-access-to-flights) | Confirms search, fare and booking capability; explains sandbox limitations and explicit production approval. |
| [Build a Flight Booking Experience](https://docs.liteapi.travel/docs/build-a-flight-booking-experience) | Describes GDS/NDC/LCC aggregation and the complete search-to-confirmed-ticket flow. |
| [Search for flights](https://docs.liteapi.travel/reference/post_flights-rates) | Documents live multi-provider search, itinerary inputs, fare output and market-sensitive fields. |
| [List airlines](https://docs.liteapi.travel/reference/get_data-flights-airlines) | Documents airline directory data and IATA/ICAO, country, alliance and active status metadata. |
| [Create a flight prebook](https://docs.liteapi.travel/reference/post_flights-prebooks) | Documents offer reservation, payment setup and available seats or baggage. |
| [Complete a flight booking](https://docs.liteapi.travel/reference/post_flights-bookings) | Documents provider finalisation, confirmed booking and provider confirmation reference. |
| [Get flight booking details](https://docs.liteapi.travel/reference/get_flights-bookings-bookingid) | Documents itinerary, passenger, booking-status, pricing and provider-reference retrieval. |
| [List flight bookings](https://docs.liteapi.travel/reference/get_flights-bookings) | Documents confirmed booking listing and airline-PNR lookup. |
| [Using Nuitee Connect webhooks](https://docs.liteapi.travel/docs/using-liteapi-webhooks) | States that flight lifecycle events are available where flight booking is enabled on the account. |

## Limitations

This review combines:

- public LiteAPI documentation available on 28 July 2026; and
- the reported observation that Qantas, Jetstar and Virgin Australia appeared in LiteAPI API and console searches.

The account responses and console screenshots were not supplied as attachments to this review, so the exact environment, routes, travel dates, point of sale, fare types and response payloads could not be independently inspected here.

Carrier inventory and downstream capabilities can vary by provider, route, date, fare, point of sale, account entitlement and production configuration. Public documentation describing general flight endpoints does not amount to a carrier-specific production guarantee.
