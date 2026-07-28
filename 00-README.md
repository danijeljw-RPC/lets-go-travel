# Consumer Travel Platform — Planning Foundation

## Purpose

This pack captures the current product and architecture thinking for a consumer travel platform built around trips, bookings, traveller services, and a private supplier-integration layer.

It is intended to be used as source material for later:

- architecture decision records;
- open issues and research questions;
- product and domain documentation;
- security and privacy documentation;
- implementation plans;
- integration specifications;
- operational runbooks;
- API and mobile-client planning.

This is not a formal decision set. It deliberately separates:

- current direction;
- working assumptions;
- vendor-dependent facts;
- unresolved questions;
- ideas that require validation.

Existing repository templates and documentation rules should be applied later. They are intentionally not reproduced here.

## Current product direction

The product is a standalone consumer offering. Its documentation and design should remain focused on consumer travel needs.

The proposed foundation is:

- .NET 10;
- ASP.NET Core Web API;
- PostgreSQL;
- Keycloak;
- a web client using the same public application API as future mobile clients;
- later native or cross-platform Android and iOS applications;
- all supplier integrations hidden behind the platform Web API;
- LiteAPI/Nuitee Connect as an initial travel inventory and booking supplier;
- the platform, rather than LiteAPI, owning customer identity, profile, trips, preferences, and product experience.

## Core architectural position

The Web API is the stable product boundary.

The web UI, Android app, and iOS app authenticate with Keycloak and send bearer access tokens to the Web API. They do not call LiteAPI directly and never receive LiteAPI credentials.

The Web API:

- validates access tokens;
- enforces authorisation;
- applies rate limits;
- owns business rules;
- controls pricing and discounts;
- stores the platform's customer and booking records;
- invokes LiteAPI and future suppliers;
- reconciles supplier booking state;
- produces notifications and audit history;
- exposes a supplier-neutral contract to every client.

## Documents in this pack

1. `01-product-vision-and-scope.md`
2. `02-target-architecture.md`
3. `03-identity-and-access.md`
4. `04-api-boundary-and-client-strategy.md`
5. `05-supplier-integration-and-liteapi.md`
6. `06-payments-pricing-discounts-and-pci.md`
7. `07-trips-bookings-and-domain-model.md`
8. `08-booking-reconciliation-and-version-history.md`
9. `09-security-privacy-and-data-ownership.md`
10. `10-operational-reliability.md`
11. `11-mobile-and-web-experience.md`
12. `12-decisions-assumptions-and-open-questions.md`
13. `13-initial-planning-roadmap.md`

The structured project documentation produced from this pack begins at [`docs/README.md`](docs/README.md). Keep the remaining root files as discovery evidence until the structured documentation is reviewed.

## How to use this pack

Treat these files as discovery input.

The first repository pass should:

- preserve the meaning;
- identify contradictions and missing decisions;
- map each topic into the repository's existing documentation system;
- avoid silently converting assumptions into decisions;
- create an ordered documentation plan;
- retain vendor verification questions as open issues;
- avoid implementation work until the planning structure is reviewed.

## Important vendor caveat

LiteAPI documentation currently uses both the LiteAPI and Nuitee Connect names. This pack uses `LiteAPI/Nuitee Connect` where clarity is useful.

The following must not be assumed until commercially and technically verified:

- exact Australian airline coverage, including Qantas, Jetstar, and Virgin Australia;
- which fares are GDS, NDC, or other content;
- post-booking change, cancellation, refund, exchange, and disruption servicing;
- whether retrieved flight bookings reflect later airline schedule changes;
- whether a schedule-change webhook exists;
- live flight-status, gate, terminal, and aircraft data;
- merchant-of-record responsibilities in the selected commercial arrangement;
- exact PCI DSS self-assessment scope for the final web and mobile implementation;
- markup constraints, parity rules, commission settlement, and discount funding.

## Terminology

- **Platform** means this consumer travel product and its services.
- **Client** means the web UI, Android app, or iOS app.
- **Supplier** means LiteAPI/Nuitee Connect or any future fulfilment provider.
- **Customer** means the authenticated platform account holder.
- **Traveller** means a person included in a trip or booking. A traveller may or may not be the account holder.
- **Trip** means the customer-facing container for flights, accommodation, activities, documents, notifications, and planning.
- **Booking** means the platform's durable representation of a supplier reservation.
- **Supplier booking** means the external reservation maintained by a supplier or carrier.
- **Reconciliation** means comparing supplier state with the platform's current state and recording meaningful changes.
