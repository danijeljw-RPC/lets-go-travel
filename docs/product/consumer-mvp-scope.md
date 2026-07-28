<!-- markdownlint-disable MD013 -->

# Consumer MVP Scope

## Status

Accepted product scope. See closed [OI-0001](../issues/closed/OI-0001-mvp-product-scope.md). Supplier, payment and servicing evidence remains in review before production activation.

## Selected MVP

The initial product is a web-first consumer booking experience supporting hotel-only, flight-only and combined hotel-plus-flight journeys. It includes trips, customer accounts, supplier/provider-controlled payment, booking records, email confirmation, ticket support and durable reconciliation. Australia is the operating focus, but customers from other countries are not geo-blocked.

## In Scope

- Keycloak-based consumer account registration and sign-in.
- Customer profile with minimal personal data.
- Trip creation before or after booking.
- Hotel destination/date/occupancy search and booking.
- Flight origin/destination/date/traveller search and booking where the provider capability is approved.
- Combined hotel-plus-flight search and trip assembly over separately represented product bookings.
- Product-specific offer detail, fare/rate rules and cancellation terms.
- Product-specific prebook revalidation.
- Approved payment component that keeps raw card data outside the platform backend.
- Hotel and flight booking confirmation and trip attachment.
- Current booking detail and immutable booking history.
- Daily active-flight reconciliation, increasing to hourly checks during the final 24 hours before each affected segment.
- Email confirmation, change notification and ticket-support access.
- Cancellation request and refund-state display where the supplier and commercial route support them.
- Locale selection with `en-AU` and AUD defaults.

## Production Activation Gates

- Reusable sensitive traveller data follows closed [OI-0008](../issues/closed/OI-0008-saved-traveller-and-passport-data.md) and cannot be activated without its protection controls.
- Customer-visible flight offers require the external evidence tracked by [OI-0003](../issues/open/OI-0003-australian-airline-and-qantas-coverage.md), [OI-0004](../issues/open/OI-0004-flight-servicing-and-schedule-changes.md), and [OI-0005](../issues/open/OI-0005-liteapi-webhook-coverage.md).
- Production payment requires the written commercial and PCI evidence tracked by [OI-0002](../issues/open/OI-0002-liteapi-commercial-and-merchant-of-record.md) and [OI-0006](../issues/open/OI-0006-mobile-payment-and-pci-scope.md).
- Mobile applications depend on the web flow and [OI-0006](../issues/open/OI-0006-mobile-payment-and-pci-scope.md).

If a route, carrier, rate or payment plan is not approved, feature controls suppress that offer rather than changing the accepted product scope or presenting an unbookable result.

## Deferred

- Native Android and iOS applications.
- Push notifications and offline trip packs.
- Live gate, terminal, aircraft, diversion and actual-movement status.
- Trip sharing and collaboration.
- Loyalty and rewards.
- Activities, rail, transfers, insurance and document vaults.
- AI itinerary planning.

## Exit Criteria

- Hotel, flight and combined journeys complete through approved provider routes.
- Australia-first global access, locale selection and currency separation follow closed OI-0007.
- Commercial/payment responsibilities are evidenced in OI-0002 and OI-0006.
- Supplier sandbox proves each enabled hotel and flight workflow.
- Ticket support, supplier escalation and failure paths are operationally proven.
