<!-- markdownlint-disable MD013 -->

# Consumer MVP Scope

## Status

Draft. [OI-0001](../issues/open/OI-0001-mvp-product-scope.md) must resolve the initial sellable product.

## Recommended MVP

The current recommendation is a web-first, hotel-first MVP for Australian consumers, with trips, customer accounts, secure payment, booking records, email confirmation and basic reconciliation. Flight discovery should proceed in parallel but flight selling should enter the MVP only after account access, Australian airline coverage, servicing, payment and schedule-change behaviour are verified.

## In Scope if the Recommendation Is Accepted

- Keycloak-based consumer account registration and sign-in.
- Customer profile with minimal personal data.
- Trip creation before or after booking.
- Hotel destination/date/occupancy search.
- Offer detail and cancellation terms.
- Prebook revalidation.
- Approved payment component that keeps raw card data outside the platform backend.
- Hotel booking confirmation and trip attachment.
- Current booking detail and immutable booking history.
- Email confirmation and support reference.
- Cancellation request and refund-state display where the supplier supports them.

## Conditional Scope

- Saved traveller profiles depend on [OI-0008](../issues/open/OI-0008-saved-traveller-and-passport-data.md).
- Flights depend on [OI-0003](../issues/open/OI-0003-australian-airline-and-qantas-coverage.md), [OI-0004](../issues/open/OI-0004-flight-servicing-and-schedule-changes.md), and [OI-0005](../issues/open/OI-0005-liteapi-webhook-coverage.md).
- Mobile applications depend on the web flow and [OI-0006](../issues/open/OI-0006-mobile-payment-and-pci-scope.md).

## Deferred

- Native Android and iOS applications.
- Push notifications and offline trip packs.
- Live flight operations.
- Trip sharing and collaboration.
- Loyalty and rewards.
- Activities, rail, transfers, insurance and document vaults.
- AI itinerary planning.

## Exit Criteria

- Product owner selects an MVP option in OI-0001.
- Launch market and currency are decided in OI-0007.
- Commercial/payment responsibilities are evidenced in OI-0002 and OI-0006.
- Supplier sandbox proves the chosen hotel workflow.
- Support and failure paths are defined.
