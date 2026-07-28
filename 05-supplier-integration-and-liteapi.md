# Supplier Integration and LiteAPI

## Integration position

LiteAPI/Nuitee Connect should be treated as a private supplier behind the platform API.

It should not own:

- platform authentication;
- the master customer profile;
- trip organisation;
- saved preferences;
- the public API contract;
- notification preferences;
- the platform's support history.

It may own or control external fulfilment records required to complete and service bookings.

## Current vendor capabilities identified from official documentation

As of July 2026, official documentation indicates that LiteAPI/Nuitee Connect provides:

- REST API access authenticated using a confidential API key in the `X-API-Key` header;
- hotel metadata, rates, prebook, booking, and booking-management capabilities;
- flight search, prebook, booking, and retrieval endpoints, subject to access;
- a user-payment option using its payment SDK;
- payment UI based on Stripe Elements;
- booking retrieval and listing endpoints;
- webhooks for documented booking lifecycle events;
- commission or markup-oriented revenue models;
- support for passing guest details during booking rather than requiring each traveller to authenticate with LiteAPI.

These capabilities must still be tested in sandbox and confirmed in the commercial agreement.

## Backend-only authentication

LiteAPI API keys must be stored only in protected server-side configuration.

They must not be:

- embedded in the web UI;
- embedded in mobile applications;
- returned by the Web API;
- committed to source control;
- written to ordinary logs;
- exposed through diagnostics.

Separate keys should be used for sandbox and production. Rotation and revocation procedures are required.

## Internal provider boundary

The platform should define supplier-neutral operations rather than mirroring endpoint names throughout the domain.

Relevant capability groups may include:

- accommodation content;
- accommodation availability;
- accommodation booking;
- flight shopping;
- flight booking;
- booking retrieval;
- cancellation;
- payment-session creation;
- webhook verification;
- document retrieval.

A single supplier does not need to implement every future capability.

## Capability discovery

Provider support should be explicit.

Examples of capabilities that may vary by supplier or market:

- refundable rates;
- multi-room booking;
- child occupancy rules;
- airline ancillaries;
- seats;
- baggage;
- frequent-flyer numbers;
- exchanges;
- cancellations;
- refunds;
- schedule-change handling;
- payment methods;
- currencies;
- pay-now versus pay-later;
- supplier-hosted checkout.

The platform should not pretend a capability exists when the supplier does not support it.

## Data mapping

The supplier integration should map external data into platform-owned models.

Mapping should preserve:

- original identifiers;
- supplier and carrier references;
- original currency;
- cancellation terms;
- source timestamps;
- expiry timestamps;
- offer identifiers;
- booking status;
- traveller association;
- itinerary detail;
- supplier payload where retention is permitted.

Unknown supplier fields should not silently corrupt the platform model.

## Search and prebook

Search results are provisional.

The design must model:

- offer expiry;
- rate revalidation;
- availability revalidation;
- price change;
- cancellation-policy change;
- tax and fee changes;
- session expiry;
- currency;
- occupancy;
- room or fare conditions.

Prebook should be treated as a separate confirmation stage before final payment and booking.

## Bookings

A platform booking should retain:

- platform booking ID;
- customer ID;
- trip ID;
- supplier;
- supplier booking ID;
- supplier or airline confirmation reference;
- current platform status;
- current supplier status;
- original booking currency and totals;
- relevant payment references;
- cancellation policy captured at booking;
- current itinerary or stay details;
- creation and last reconciliation timestamps.

## Webhooks

Webhook handling must assume:

- duplicate delivery;
- out-of-order delivery;
- delayed delivery;
- missing delivery;
- invalid signatures or credentials;
- retries;
- payload version changes;
- events arriving after local state has changed.

A webhook should normally trigger durable event recording and reconciliation rather than blindly overwrite booking state.

## Unresolved flight-change capability

Official documentation confirms that flight bookings can be listed and retrieved. It does not, from the material reviewed for this pack, establish that:

- later airline schedule changes are always written back into the retrievable booking;
- a dedicated schedule-change webhook is provided;
- live operational changes such as gate, terminal, aircraft, and delay are included;
- involuntary changes can be accepted or rejected through the API.

This is a critical vendor question.

Reconciliation only works when the supplier's retrieved booking reflects the changed source state.

## Australian airline coverage

Do not record Qantas, Jetstar, Virgin Australia, or any other airline as supported until LiteAPI confirms:

- domestic coverage;
- international coverage;
- content source;
- fare families;
- ancillaries;
- frequent-flyer handling;
- servicing;
- schedule changes;
- cancellations and refunds;
- production availability for the platform's account.

## Supplier exit strategy

The platform should be able to add another supplier without replacing:

- Keycloak;
- customer accounts;
- trips;
- saved travellers;
- public identifiers;
- notification preferences;
- support history.

The supplier abstraction should not force artificial uniformity. Some supplier-specific metadata will remain necessary and should be isolated.

## Official sources reviewed

- https://docs.liteapi.travel/reference/authentication
- https://docs.liteapi.travel/docs/user-payment
- https://docs.liteapi.travel/reference/post_rates-prebook
- https://docs.liteapi.travel/reference/post_rates-book
- https://docs.liteapi.travel/reference/post_flights-rates
- https://docs.liteapi.travel/reference/post_flights-prebooks
- https://docs.liteapi.travel/reference/post_flights-bookings
- https://docs.liteapi.travel/reference/get_flights-bookings
- https://docs.liteapi.travel/docs/using-liteapi-webhooks
- https://docs.liteapi.travel/docs/revenue-management-and-commission
