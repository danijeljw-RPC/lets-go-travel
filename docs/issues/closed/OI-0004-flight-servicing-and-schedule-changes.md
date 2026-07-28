---
issue_id: OI-0004
title: Verify Flight Servicing and Schedule-Change Behaviour
status: closed
type: integration-question
priority: p0
severity: critical
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Product
  - Operations
  - Supplier Integration
  - Architecture
related_adrs:
  - ADR-0004
  - ADR-0005
  - ADR-0008
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
  - docs/integrations/liteapi-nuitee-connect.md
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0004 — Verify Flight Servicing and Schedule-Change Behaviour

## Summary

Define the supported flight-servicing boundary after confirmation, including retrieval freshness, schedule-change detection, cancellation, exchanges, refunds, manual escalation, support availability, fees and service-level limitations.

## Decision

**Option B is selected: LiteAPI supports booking and some servicing capabilities, but the platform must treat post-booking servicing as partly manual and capability-gated.**

The issue is closed because the safe product and operational boundary is now defined. Closure does not assert that LiteAPI provides complete automated servicing or guaranteed source-current airline schedule data.

The platform may sell flights only after the production account and required carriers have passed the entitlement and capability evidence defined by OI-0003. For an eligible booking:

- readytogo.travel retrieves and displays the latest booking record available from LiteAPI.
- readytogo.travel monitors active bookings using the reconciliation process accepted in ADR-0008.
- LiteAPI webhooks may trigger immediate reconciliation when a documented flight lifecycle event is received.
- The platform does not promise that LiteAPI retrieval is a real-time airline operational-status feed.
- The platform does not expose self-service exchange, name change, partial cancellation or partial refund unless that exact capability has been proven for the carrier, provider, fare and production account.
- Unsupported or uncertain servicing requests are handled through an internal case and escalated to LiteAPI.
- Customer-facing wording must distinguish confirmed itinerary information from live airline operational status and must direct travellers to the operating airline for immediate day-of-travel operational confirmation.

## Closure rationale

The original issue correctly identified that create, list and retrieve endpoints do not prove airline-source freshness, schedule-change propagation or full servicing automation.

LiteAPI's current public documentation adds enough evidence to define a safe minimum operating model:

1. confirmed flight bookings can be retrieved by LiteAPI booking ID;
2. confirmed bookings can be listed and looked up using airline PNR and passenger surname;
3. non-confirmed bookings may require polling because some providers update the booking asynchronously;
4. flight lifecycle webhooks exist for prebook, ancillary attachment, created, pending confirmation, confirmed, cancelled, failed and expired states;
5. the public webhook event list does not identify a dedicated flight schedule-change, involuntary-change, exchange or refund event;
6. LiteAPI publicly prices voluntary flight modifications, including date changes, name changes, exchanges and partial cancellations/refunds, at EUR 25 per modification;
7. LiteAPI states that booking-information requests, automated full-refund cancellations and corrections caused by provider or supplier system errors do not incur the EUR 25 servicing fee;
8. LiteAPI exposes dashboard ticketing and public support channels, describes support as available 24/7, and states that developer-support responses are typically provided within 24 hours; and
9. no public flight-specific contractual acknowledgement or resolution SLA has been identified.

The evidence supports a partly manual operating model. It does not support advertising universal real-time schedule propagation, instant exchange/refund automation or a guaranteed supplier resolution time.

## Capability matrix

| Capability | Status | Public evidence | Platform treatment |
| --- | --- | --- | --- |
| Retrieve a confirmed booking by LiteAPI booking ID | Supported | `GET /v3.0/flights/bookings/{bookingId}` returns the complete confirmed booking record, itinerary, passengers, booking status, pricing and provider confirmation reference. | Use for customer itinerary display, support investigation and reconciliation. |
| List confirmed bookings | Supported | `GET /v3.0/flights/bookings` returns confirmed bookings owned by the authenticated account. | Use for account-scoped booking retrieval and reconciliation. |
| Retrieve using airline PNR and surname | Supported | The list endpoint supports an `airlinePnr` plus passenger `lastName` lookup. | Use as a secondary lookup and support tool. |
| Initial confirmation-state freshness | Supported with polling | LiteAPI recommends polling the GET booking endpoint where a booking is not yet `CONFIRMED`, because some providers update status asynchronously. | Poll pending bookings until terminal state under the booking workflow. |
| Post-confirmation airline-source freshness | Unproven | The retrieval documentation describes the LiteAPI booking record but does not state that every retrieval refreshes directly from the airline, GDS, NDC or LCC source. | Treat retrieval as the latest supplier-visible record, not a guaranteed live operational feed. Reconcile under ADR-0008. |
| Flight lifecycle webhooks | Supported where flight booking is enabled | Documented events include `flight.prebook`, `flight.attachServices`, `flight.book.created`, `flight.book.pending.confirmation`, `flight.book.confirmed`, `flight.book.cancelled`, `flight.book.failed` and `flight.book.expired`. | Consume idempotently and trigger immediate reconciliation where relevant. |
| Dedicated schedule-change webhook | Not publicly documented | The published flight event list does not identify a schedule-change or itinerary-change event. | Do not depend solely on webhooks for schedule changes. Continue scheduled reconciliation. |
| Schedule-change propagation through retrieval | Unproven | No public guarantee or controlled production test has been recorded showing how quickly airline schedule changes appear in booking retrieval. | Do not promise real-time propagation. Record any retrieved differences as immutable itinerary versions and notify according to ADR-0008. |
| Voluntary date change | Manual vendor servicing by default | LiteAPI's pricing page classifies date changes as voluntary booking modifications subject to a EUR 25 servicing fee. No public self-service flight-change endpoint was identified. | Create a servicing case and escalate to LiteAPI unless an account-specific automated capability is later proven. |
| Passenger name change or correction | Manual vendor servicing by default | Name changes are expressly included in the EUR 25 voluntary modification fee. Provider/supplier-error corrections are excluded from the servicing fee. | Escalate. Do not promise that a carrier permits a name change. Confirm fare/carrier rules and customer cost before action. |
| Exchange | Manual vendor servicing by default | Exchanges are expressly included in the EUR 25 voluntary modification fee. No public flight-exchange endpoint was identified. | Escalate and obtain a quoted fare difference, taxes, supplier charges and LiteAPI fee before customer approval. |
| Partial cancellation | Manual vendor servicing by default | Partial cancellations/refunds are included in the EUR 25 voluntary modification fee. | Escalate. Do not expose universal self-service partial cancellation. |
| Partial refund | Manual vendor servicing by default | Partial cancellations/refunds are included in the EUR 25 voluntary modification fee. | Escalate and record the quoted refundable amount, supplier penalties and servicing fee before approval. |
| Automated full-refund cancellation | Supported in some cases, capability-gated | LiteAPI states that automated full-refund cancellations do not incur the flight servicing fee. The public material reviewed does not establish universal eligibility or a documented public flight-cancellation endpoint. | Offer only where the production response or account-specific documentation proves the booking is eligible and the action is available. Otherwise escalate. |
| Non-refundable or penalised cancellation | Fare/provider dependent and manual by default | Public flight documentation does not provide a universal cancellation/refund promise. | Retrieve applicable fare conditions and escalate for a confirmed quote before customer commitment. |
| Involuntary change or supplier correction | Manual vendor servicing by default | LiteAPI states that corrections required because of provider or supplier system errors do not incur the EUR 25 servicing fee. | Open a priority servicing case, record the airline/provider event and request available re-accommodation or refund options. |
| Flight booking information request | Supported without servicing fee | LiteAPI states that booking-information requests do not incur the flight servicing fee. | Operations may retrieve the record and escalate questions without charging the LiteAPI modification fee. |
| Day-of-travel operational status | Not established as a LiteAPI capability | The reviewed booking endpoints provide itinerary and booking status, not a public guarantee of live departure, delay, gate or disruption data. | Direct the traveller to the operating airline for immediate operational confirmation. Do not market LiteAPI retrieval as flight tracking. |

## Retrieval and reconciliation boundary

ADR-0008 remains the authoritative platform-monitoring decision.

A dedicated private .NET 10 worker:

- reconciles active flight bookings daily;
- increases to hourly checks during the final 24 hours before each affected segment;
- records meaningful immutable itinerary versions under ADR-0004;
- triggers customer notifications from detected changes; and
- may perform an immediate check when a LiteAPI webhook is received.

This process establishes the platform's detection and audit mechanism. It cannot make the upstream LiteAPI booking record fresher than LiteAPI and its connected provider.

The following distinction must be preserved:

- **Supplier-visible booking state:** the latest state returned by LiteAPI or received through a LiteAPI webhook.
- **Platform-observed state:** the most recently reconciled state recorded by readytogo.travel.
- **Live airline operational state:** the airline's current operational information, which may need to be confirmed directly with the operating carrier.

## Schedule-change handling

No dedicated flight schedule-change webhook was identified in LiteAPI's public event catalogue.

The platform must therefore use the following sequence when it detects an itinerary difference or receives a customer/airline report:

1. Retrieve the booking through LiteAPI.
2. Compare the returned journey, segments, timings, flight numbers, airports and status against the latest immutable platform version.
3. Record a new immutable version only where the difference is meaningful under ADR-0004.
4. Classify the change as informational, minor, material or travel-blocking.
5. Notify the customer according to the customer-notification rules in ADR-0008.
6. Where acceptance, rebooking, exchange, refund or carrier action is required, open a servicing case.
7. Escalate the case to LiteAPI with the booking ID, airline PNR, affected passengers, segment details, requested outcome and sanitised evidence.
8. Do not represent the matter as resolved until LiteAPI or the airline returns a confirmed result and the booking is reconciled again.

## Manual servicing workflow

### Internal owner

**Operations** owns the customer case from intake to closure.

**Supplier Integration** owns vendor escalation, technical evidence and supplier follow-up.

**Product** owns customer-facing limitations and determines which servicing actions may be exposed as self-service.

**Architecture** owns the reconciliation, audit and capability-gating controls.

### Required case record

Each servicing case must contain:

- readytogo.travel booking identifier;
- LiteAPI booking identifier;
- airline PNR or provider confirmation reference;
- marketing and operating carrier;
- affected passenger or passengers;
- affected segment or segments;
- current stored itinerary version;
- latest LiteAPI retrieval timestamp and sanitised response evidence;
- requested action;
- fare rules or known restrictions;
- customer approval for quoted costs;
- LiteAPI dashboard ticket identifier;
- communication history;
- supplier outcome; and
- final reconciliation evidence.

### LiteAPI escalation channels

The public channels identified are:

1. **LiteAPI dashboard:** use the account's **Request Assistance** or support-ticket feature.
2. **Developer or integration issue:** submit through the developer support portal or email `dev-support@nuitee.com`.
3. **General booking/customer support:** use the customer-support path on the Nuitée contact page.

For a booking-servicing matter, the dashboard ticket is the system-of-record escalation channel. Developer support should be used where an API defect, missing event, inconsistent response or integration fault is involved.

No secret API key, full payment credential, unrestricted identity document or unnecessary passenger personal information should be placed in an email or support ticket.

## Support hours and SLA

### Publicly documented availability

Nuitée's public contact page describes support as **24/7**.

### Publicly documented technical response target

The same page states that developer-support response times are **typically within 24 hours for technical issues**.

### Limitation

The public wording does not establish:

- a contractual flight-servicing SLA;
- severity definitions;
- acknowledgement targets for urgent travel disruption;
- resolution times;
- rebooking or refund completion targets;
- an escalation manager or telephone number;
- airline operating-hours coverage;
- compensation for missed SLA; or
- guaranteed response or resolution for a specific account.

Accordingly:

- `24/7` is recorded as the published support-availability statement;
- `typically within 24 hours` is recorded as an indicative technical-response statement, not a contractual commitment;
- readytogo.travel must not promise a supplier resolution deadline based only on this public wording; and
- account-specific commercial terms may replace or strengthen this position and should be recorded under the LiteAPI commercial review.

## Fees

LiteAPI's public pricing page states the following flight charges unless overridden by a commercial agreement:

| Item | Public price | Treatment |
| --- | ---: | --- |
| Flight ticketing fee | 1% of total transaction value, minimum EUR 2 and maximum EUR 10 per booking | Booking charge. Record against the booking commercial model. |
| Voluntary booking modification | EUR 25 per modification | Applies to date changes, name changes, exchanges and partial cancellations/refunds. |
| Booking information request | No flight booking servicing fee | Operations may retrieve or request information without the EUR 25 modification fee. |
| Automated full-refund cancellation | No flight booking servicing fee | Applies only where the cancellation is actually supported and qualifies for a full refund. |
| Provider or supplier system-error correction | No flight booking servicing fee | Applies to corrections required because of upstream error. Other fare differences or supplier costs may still require confirmation. |

The public pricing page states that custom commercial agreements may override standard pricing. Customer-facing fees must therefore be generated from the approved commercial configuration and confirmed servicing quote, not hard-coded from this document.

## Customer-facing limitations

The following limitations are approved as the minimum safe flight promise:

1. Flight bookings and carrier inventory are available only where production capability has been approved under OI-0003.
2. readytogo.travel displays the latest itinerary and booking status available to the platform; this is not represented as a live airline flight-tracking service.
3. Schedule-change notifications depend on the change becoming visible through LiteAPI, a webhook, an airline/customer report or another approved source.
4. Customers should confirm immediate day-of-travel operational information with the operating airline.
5. Exchanges, name changes, partial cancellations and partial refunds are request-based services, not guaranteed self-service actions.
6. Eligibility, fare difference, supplier penalty, taxes and servicing fees must be confirmed before a voluntary change is committed.
7. Full refunds are offered only where the fare, provider and available LiteAPI servicing path confirm eligibility.
8. Involuntary changes and supplier errors are escalated for available re-accommodation, correction or refund options; a particular remedy cannot be promised before supplier confirmation.
9. Support may require direct airline involvement where LiteAPI cannot perform the requested action.
10. No supplier acknowledgement or resolution time is promised unless account-specific contractual terms establish it.

## Implementation requirements

- Maintain a servicing-capability registry by environment, carrier, provider and action.
- Treat retrieval, cancellation, refund, exchange, name change and schedule-change propagation as separate capabilities.
- Default uncertain servicing capabilities to `Manual` or `Unsupported`, never `Supported`.
- Consume LiteAPI flight webhooks idempotently using `event_id`.
- Parse the webhook `request` and `response` fields as stringified JSON.
- Distinguish sandbox and production events using the webhook `sandbox` field.
- Poll non-confirmed bookings as recommended by LiteAPI.
- Continue ADR-0008 reconciliation after confirmation.
- Store every material itinerary change as an immutable version.
- Require customer approval before any quoted voluntary servicing cost is incurred.
- Record vendor ticket numbers and servicing outcomes.
- Reconcile the final LiteAPI booking state before closing the customer case.
- Feature-gate every customer-facing servicing action that has not been proven end to end.

## Evidence reviewed

### Internal evidence

- Product-owner flight-monitoring direction recorded on 2026-07-28.
- ADR-0004 — immutable itinerary and booking version history.
- ADR-0005 — reconciliation and operational flight-status boundary.
- ADR-0008 — durable flight reconciliation and customer notification.
- OI-0003 findings that production flight access and carrier capability require account-level and carrier-specific evidence.
- The previous OI-0004 record, which correctly identified the missing post-booking servicing evidence.

### LiteAPI public evidence

- [Getting Access to Flights](https://docs.liteapi.travel/docs/getting-access-to-flights)
- [Build a Flight Booking Experience](https://docs.liteapi.travel/docs/build-a-flight-booking-experience)
- [Get booking details](https://docs.liteapi.travel/reference/get_flights-bookings-bookingid)
- [List flight bookings](https://docs.liteapi.travel/reference/get_flights-bookings)
- [Using Nuitee Connect webhooks](https://docs.liteapi.travel/docs/using-liteapi-webhooks)
- [API Pricing and Usage Costs](https://docs.liteapi.travel/reference/api-pricing-usage-costs)
- [Contact Nuitée](https://nuitee.com/contact)

## Evidence interpretation

The LiteAPI public documentation proves that booking retrieval, PNR lookup, asynchronous confirmation polling, flight lifecycle webhooks and a priced servicing model exist.

The public documentation does not prove:

- that every booking retrieval refreshes directly from the airline or underlying provider;
- a maximum delay for schedule-change propagation;
- a dedicated flight schedule-change event;
- a public self-service flight exchange endpoint;
- a public self-service partial-refund endpoint;
- universal automated cancellation eligibility;
- flight-specific support contact details beyond the general dashboard and support channels; or
- a contractual servicing acknowledgement or resolution SLA.

These gaps are not treated as implicit support. They are resolved by selecting the manual, capability-gated boundary in Option B.

## Decision impact

OI-0004 no longer independently blocks provider-neutral flight implementation or production flight sales once OI-0003 and the other production gates are satisfied.

It does block:

- advertising real-time airline schedule propagation;
- advertising universal self-service flight changes or refunds;
- exposing an untested servicing action;
- promising a LiteAPI resolution time not established in account-specific terms; and
- treating the LiteAPI booking endpoint as a live flight-status source.

## Acceptance criteria

- [x] Each servicing capability has a supported, unsupported, capability-gated or manual status.
- [x] Retrieval freshness and schedule-change propagation are explicitly bounded by available evidence.
- [x] Manual escalation channels, workflow and internal owners are documented.
- [x] Published support availability, indicative technical response time and flight-servicing fees are recorded.
- [x] Customer-facing limitations are approved as part of the selected direction.
- [x] Production flight sales remain gated by OI-0003 rather than by an unsupported full-servicing assumption.

## Closure statement

**OI-0004 is closed with Option B selected.**

LiteAPI is treated as supporting flight booking retrieval and a combination of automated and manually serviced post-booking actions. readytogo.travel will provide durable monitoring, reconciliation and notification, while exposing only servicing actions proven for the applicable production account, provider, carrier and fare.

The issue may be reopened if:

- LiteAPI supplies account-specific servicing terms that materially change the boundary;
- a controlled production test proves or disproves schedule-change propagation;
- new flight servicing endpoints or webhook events are published;
- the required customer promise expands to include guaranteed self-service exchanges, refunds or real-time operational status; or
- the operating support model cannot meet the approved manual escalation workflow.

## Related documents

- [Booking Reconciliation and Version History](../../domain/booking-reconciliation-and-version-history.md)
- [ADR-0004](../../adr/accepted/ADR-0004-booking-current-state-and-immutable-history.md)
- [ADR-0005](../../adr/accepted/ADR-0005-reconciliation-and-operational-flight-status-boundary.md)
- [ADR-0008](../../adr/accepted/ADR-0008-durable-flight-reconciliation-and-customer-notification.md)
